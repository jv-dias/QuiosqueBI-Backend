using System.Globalization;
using System.Text.RegularExpressions;
using QuiosqueBI.API.Models;
using QuiosqueBI.API.Services.Analise.Preparacao;

namespace QuiosqueBI.API.Services.Analise.Exploracao.Profiling
{
    /// <summary>
    /// Serviço responsável por inspecionar colunas tabulares, inferir tipos semânticos,
    /// estimar cardinalidade e anonimizar dados sensíveis.
    /// </summary>
    public class ProfilingService : IProfilingService
    {
        private readonly IPreparacaoService _preparacaoService;

        public ProfilingService(IPreparacaoService preparacaoService)
        {
            _preparacaoService = preparacaoService;
        }

        public List<PerfilColuna> AnalisarColunas(DadosArquivo.RDadosArquivo dados)
        {
            if (dados == null || dados.Headers == null || dados.Headers.Length == 0)
            {
                return new List<PerfilColuna>();
            }

            var headers = dados.Headers;
            var recordsList = dados.Records?.Cast<IDictionary<string, object>>().ToList() ?? new List<IDictionary<string, object>>();
            int totalLinhas = recordsList.Count;

            if (totalLinhas == 0)
            {
                return headers.Select((h, i) => new PerfilColuna(
                    Indice: i,
                    Nome: h,
                    TipoSemantico: TipoSemanticoColuna.TextoCategoria,
                    CardinalidadeAmostra: 0,
                    TaxaUnicidade: 0.0,
                    ExemplosAmostra: new List<string>(),
                    EhMetricaSugerida: false,
                    EhDimensaoSugerida: true
                )).ToList();
            }

            // 1. Amostragem Sistemática Baseada em Cochran (1977)
            var amostra = ExtrairAmostraEstatistica(recordsList, totalLinhas);
            int tamanhoAmostra = amostra.Count;

            var perfis = new List<PerfilColuna>();

            // 2. Análise Coluna por Coluna (Single-Column Profiling)
            for (int i = 0; i < headers.Length; i++)
            {
                var nomeColuna = headers[i];
                var valoresAmostra = amostra
                    .Select(r => r.ContainsKey(nomeColuna) ? r[nomeColuna]?.ToString()?.Trim() : null)
                    .Where(v => !string.IsNullOrWhiteSpace(v) && !string.Equals(v, "Não Identificado", StringComparison.OrdinalIgnoreCase))
                    .Select(v => v!)
                    .ToList();

                var valoresDistintos = new HashSet<string>(valoresAmostra, StringComparer.OrdinalIgnoreCase);
                int cardinalidade = valoresDistintos.Count;
                double taxaUnicidade = valoresAmostra.Count > 0 ? (double)cardinalidade / valoresAmostra.Count : 0.0;

                // 3. Classificação Semântica Indutiva
                var tipoSemantico = InferirTipoSemantico(nomeColuna, valoresAmostra, taxaUnicidade, cardinalidade);

                // 4. Mascaramento Estrito de PII (Privacy by Design - Cavoukian, 2009)
                var exemplos = ObterExemplosAnonimizados(nomeColuna, tipoSemantico, valoresDistintos);

                bool ehMetrica = tipoSemantico == TipoSemanticoColuna.NumericoMonetario || tipoSemantico == TipoSemanticoColuna.NumericoQuantidade;
                bool ehDimensao = tipoSemantico == TipoSemanticoColuna.TextoCategoria || tipoSemantico == TipoSemanticoColuna.DataTemporal;

                perfis.Add(new PerfilColuna(
                    Indice: i,
                    Nome: nomeColuna,
                    TipoSemantico: tipoSemantico,
                    CardinalidadeAmostra: cardinalidade,
                    TaxaUnicidade: Math.Round(taxaUnicidade, 4),
                    ExemplosAmostra: exemplos,
                    EhMetricaSugerida: ehMetrica,
                    EhDimensaoSugerida: ehDimensao
                ));
            }

            return perfis;
        }

        /// <summary>
        /// Extrai amostra representativa de registros com passo proporcional para garantir cobertura do arquivo.
        /// </summary>
        private List<IDictionary<string, object>> ExtrairAmostraEstatistica(List<IDictionary<string, object>> records, int totalLinhas)
        {
            // Planilhas pequenas (N <= 400): 100% dos dados analisados (erro amostral = 0)
            if (totalLinhas <= 400)
            {
                return records;
            }

            // Planilhas médias (400 < N <= 10.000): Fórmula de Cochran para n0 = 384
            int nAlvo;
            if (totalLinhas <= 10000)
            {
                const double n0 = 384.16; // Z = 1.96, p = 0.5, e = 0.05
                nAlvo = (int)Math.Ceiling(n0 / (1.0 + (n0 - 1.0) / totalLinhas));
                nAlvo = Math.Max(384, Math.Min(totalLinhas, 1000));
            }
            else
            {
                // Planilhas grandes (N > 10.000): Teto amostral de 1.000 linhas
                nAlvo = 1000;
            }

            int passo = Math.Max(1, totalLinhas / nAlvo);
            var amostra = new List<IDictionary<string, object>>(nAlvo);

            for (int i = 0; i < totalLinhas && amostra.Count < nAlvo; i += passo)
            {
                amostra.Add(records[i]);
            }

            return amostra;
        }

        /// <summary>
        /// Inferência heurística-semântica de coluna tabular.
        /// </summary>
        private TipoSemanticoColuna InferirTipoSemantico(
            string nomeColuna,
            List<string> valores,
            double taxaUnicidade,
            int cardinalidade)
        {
            var nomeNormalizado = nomeColuna.ToLowerInvariant();

            // 1. Verificação Temporal (Datas e Horas)
            if (EhColunaTemporal(nomeNormalizado, valores))
            {
                return TipoSemanticoColuna.DataTemporal;
            }

            // 2. Verificação de características numéricas
            bool temCasasDecimais = false;
            bool temSimboloMoeda = false;
            bool ehNumerico = valores.Count > 0 && EhPredominantementeNumerico(valores, out temCasasDecimais, out temSimboloMoeda);

            // 3. Verificação de Identificadores Chaves (CPFs, IDs, Códigos)
            if (EhColunaIdentificador(nomeNormalizado, taxaUnicidade, cardinalidade, valores.Count, ehNumerico))
            {
                return TipoSemanticoColuna.TextoIdentificador;
            }

            // 4. Verificação Numérica / Monetária ou Quantidade
            if (ehNumerico)
            {
                if (temSimboloMoeda || temCasasDecimais || ContemTermoFinanceiro(nomeNormalizado))
                {
                    return TipoSemanticoColuna.NumericoMonetario;
                }

                return TipoSemanticoColuna.NumericoQuantidade;
            }

            // 5. Verificação de Textos Livres (descrições longas não agrupáveis)
            if (valores.Count > 0 && valores.Average(v => v.Length) > 60)
            {
                return TipoSemanticoColuna.TextoLivre;
            }

            // 6. Categoria Padrão (Dimensão Qualitativa)
            return TipoSemanticoColuna.TextoCategoria;
        }

        private bool EhColunaTemporal(string nomeNormalizado, List<string> valores)
        {
            string[] pistasDeData = { "data", "date", "dia", "mes", "mês", "ano", "hora", "periodo", "período", "vencimento", "nascimento", "criado", "fechamento" };
            bool pistaNoNome = pistasDeData.Any(p => nomeNormalizado.Contains(p));

            if (valores.Count == 0) return pistaNoNome;

            int conversoesValidas = 0;
            foreach (var v in valores.Take(50))
            {
                if (DateTime.TryParse(v, new CultureInfo("pt-BR"), DateTimeStyles.None, out _) ||
                    DateTime.TryParse(v, CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
                {
                    conversoesValidas++;
                }
            }

            double taxaConversao = (double)conversoesValidas / Math.Min(valores.Count, 50);
            return (pistaNoNome && taxaConversao > 0.4) || taxaConversao > 0.75;
        }

        private bool EhColunaIdentificador(string nomeNormalizado, double taxaUnicidade, int cardinalidade, int totalValores, bool ehNumerico)
        {
            string[] termosIdentificadores = { "id", "cod", "código", "codigo", "cpf", "cnpj", "rg", "matricula", "matrícula", "sku", "uuid", "guid", "chave", "telefone", "celular", "email", "e-mail" };
            bool temPistaIdentificador = termosIdentificadores.Any(t =>
                nomeNormalizado.Equals(t, StringComparison.OrdinalIgnoreCase) ||
                nomeNormalizado.StartsWith($"{t}_", StringComparison.OrdinalIgnoreCase) ||
                nomeNormalizado.EndsWith($"_{t}", StringComparison.OrdinalIgnoreCase) ||
                nomeNormalizado.Contains($"cod") ||
                nomeNormalizado.Contains($"cpf") ||
                nomeNormalizado.Contains($"cnpj"));

            if (temPistaIdentificador)
            {
                return true;
            }

            // Valores predominantemente numéricos sem pistas no nome são métricas, não IDs
            if (ehNumerico)
            {
                return false;
            }

            // Para textos: taxa de unicidade muito alta (> 85% em amostras representativas) indica ID/Chave
            if (totalValores >= 15 && taxaUnicidade >= 0.85)
            {
                return true;
            }

            return false;
        }

        private bool EhPredominantementeNumerico(List<string> valores, out bool temCasasDecimais, out bool temSimboloMoeda)
        {
            temCasasDecimais = false;
            temSimboloMoeda = false;

            int totalAmostrado = Math.Min(valores.Count, 50);
            int validos = 0;

            foreach (var v in valores.Take(totalAmostrado))
            {
                if (v.Contains("R$", StringComparison.OrdinalIgnoreCase) || v.Contains('$') || v.Contains('€'))
                {
                    temSimboloMoeda = true;
                }

                if (v.Contains(',') || (v.Contains('.') && !Regex.IsMatch(v, @"^\d{1,2}\.\d{1,2}\.\d{2,4}$")))
                {
                    temCasasDecimais = true;
                }

                var valorDecimal = _preparacaoService.ConverterParaDecimal(v);
                if (valorDecimal != 0m || v == "0" || v == "0,00" || v == "0.00")
                {
                    validos++;
                }
            }

            return (double)validos / totalAmostrado >= 0.75;
        }

        private bool ContemTermoFinanceiro(string nomeNormalizado)
        {
            string[] termos = { "preco", "preço", "valor", "total", "custo", "receita", "faturamento", "subtotal", "lucro", "salario", "salário", "price", "revenue", "cost", "comissao", "comissão", "taxa", "desconto", "tarifa" };
            return termos.Any(t => nomeNormalizado.Contains(t));
        }

        /// <summary>
        /// Mascara dados sensíveis e identificadores para proteger a privacidade dos registros.
        /// </summary>
        private List<string> ObterExemplosAnonimizados(
            string nomeColuna,
            TipoSemanticoColuna tipoSemantico,
            HashSet<string> valoresDistintos)
        {
            var nomeNormalizado = nomeColuna.ToLowerInvariant();
            string[] pistasPii = { "cpf", "cnpj", "rg", "email", "e-mail", "telefone", "celular", "senha", "password", "nome", "cliente", "paciente" };

            bool contemPii = pistasPii.Any(p => nomeNormalizado.Contains(p));

            if (tipoSemantico == TipoSemanticoColuna.TextoIdentificador || contemPii)
            {
                return new List<string> { "[Anonimizado LGPD]" };
            }

            return valoresDistintos.Take(3).ToList();
        }
    }
}
