using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Mscc.GenerativeAI;
using QuiosqueBI.API.Models;
using QuiosqueBI.API.Services.Analise.Exploracao.Profiling;
using QuiosqueBI.API.Services.Analise.Preparacao;

namespace QuiosqueBI.API.Services.Analise.Exploracao
{
    public class ExploracaoService : IExploracaoService
    {
        private readonly IConfiguration _configuration;
        private readonly IPreparacaoService _preparacaoService;
        private readonly IProfilingService _profilingService;

        public ExploracaoService(
            IConfiguration configuration,
            IPreparacaoService preparacaoService,
            IProfilingService profilingService)
        {
            _configuration = configuration;
            _preparacaoService = preparacaoService;
            _profilingService = profilingService;
        }

        public async Task<List<ResultadoGrafico>> GerarInsightsIniciaisAsync(DadosArquivo.RDadosArquivo dadosDoArquivo, string contexto)
        {
            // Descoberta de metadados e perfilamento semântico prévio
            var perfilColunas = _profilingService.AnalisarColunas(dadosDoArquivo);

            var planoDeAnalise = await ObterPlanoDeAnaliseAsync(dadosDoArquivo.Headers, perfilColunas, contexto);
            var resultadosFinais = new List<ResultadoGrafico>();

            if (planoDeAnalise != null)
            {
                var recordsList = dadosDoArquivo.Records.ToList();
                foreach (var analise in planoDeAnalise)
                {
                    var cabecalhoDimensaoReal = dadosDoArquivo.Headers[analise.indice_dimensao];
                    var cabecalhoMetricaReal = dadosDoArquivo.Headers[analise.indice_metrica];

                    var dadosAgrupados = recordsList
                        .AsParallel()
                        .GroupBy(rec => (object)((IDictionary<string, object>)rec)[cabecalhoDimensaoReal])
                        .Select(g => new
                        {
                            Categoria = g.Key,
                            Valor = g.Sum(rec => _preparacaoService.ConverterParaDecimal(((IDictionary<string, object>)rec)[cabecalhoMetricaReal]))
                        })
                        .ToList();

                    if (cabecalhoDimensaoReal.ToLower().Contains("data"))
                    {
                        // ... Lógica de ordenação e formatação de datas ...
                    }
                    else
                    {
                        dadosAgrupados = dadosAgrupados.OrderByDescending(x => x.Valor).ToList();
                    }

                    resultadosFinais.Add(new ResultadoGrafico
                    {
                        Titulo = analise.titulo_grafico,
                        TipoGrafico = analise.tipo_grafico,
                        Dados = dadosAgrupados
                    });
                }
            }
            return resultadosFinais;
        }

        private async Task<List<AnaliseSugerida>?> ObterPlanoDeAnaliseAsync(
            string[] headers,
            List<PerfilColuna> perfilColunas,
            string contexto)
        {
            var apiKey = _configuration["Gemini:ApiKey"] ?? throw new InvalidOperationException("Chave da API do Gemini não configurada.");

            var googleAi = new GoogleAI(apiKey);
            var model = googleAi.GenerativeModel(Model.Gemini25Flash);

            // Serialização compacta em marcadores semânticos para economia de tokens
            var linhasMetadados = new StringBuilder();
            foreach (var perfil in perfilColunas)
            {
                var papelSemantico = perfil.TipoSemantico switch
                {
                    TipoSemanticoColuna.NumericoMonetario => "Métrica Numérica (Moeda/Financeiro)",
                    TipoSemanticoColuna.NumericoQuantidade => "Métrica Numérica (Quantidade/Contagem)",
                    TipoSemanticoColuna.DataTemporal => "Dimensão Temporal (Data/Período)",
                    TipoSemanticoColuna.TextoCategoria => $"Dimensão Categórica (Valores únicos na amostra: {perfil.CardinalidadeAmostra})",
                    TipoSemanticoColuna.TextoIdentificador => "Identificador/Código/Chave [REGRA CRÍTICA: NUNCA USAR COMO MÉTRICA DE SOMA]",
                    TipoSemanticoColuna.TextoLivre => "Texto Livre/Narrativo (Não recomendado para agrupamento)",
                    _ => "Outro"
                };

                var exemplosFormatados = perfil.ExemplosAmostra != null && perfil.ExemplosAmostra.Count > 0
                    ? string.Join(", ", perfil.ExemplosAmostra.Select(e => $"\"{e}\""))
                    : "nenhum";

                linhasMetadados.AppendLine($"- Coluna [{perfil.Indice}]: \"{perfil.Nome}\" | Papel: {papelSemantico} | Exemplos: [{exemplosFormatados}]");
            }

            var promptText = $$"""
                               Você é um motor analítico e planejador de Business Intelligence (BI).
                               Sua missão é planejar análises de dados altamente relevantes cruzando dimensões e métricas compatíveis.

                               OBJETIVO DE NEGÓCIO DO USUÁRIO:
                               "{{contexto}}"

                               METADADOS E PERFIL SEMÂNTICO DAS COLUNAS DISPONÍVEIS:
                               {{linhasMetadados.ToString().TrimEnd()}}

                               DIRETRIZES ANALÍTICAS OBRIGATÓRIAS:
                               1. REGRA DE OURO (MÉTRICAS): NUNCA utilize colunas com papel de 'Identificador' (como IDs, códigos, CPFs) ou 'Texto Livre' como 'indice_metrica'. Apenas colunas de Métrica Numérica (Moeda ou Quantidade) podem ser somadas.
                               2. COMPATIBILIDADE DE GRÁFICOS:
                                  - 'linha': Use preferencialmente quando 'indice_dimensao' for uma coluna Temporal (Data/Período).
                                  - 'pizza': Use apenas para colunas Categóricas com baixa cardinalidade (<= 7 categorias distintas).
                                  - 'barras': Ideal para comparações entre categorias e métricas numéricas.
                               3. Sugira até 10 análises estratégicas que respondam diretamente ao objetivo do usuário.
                               4. Retorne EXCLUSIVAMENTE um array JSON válido contendo objetos no formato abaixo (sem texto conversacional antes ou depois):

                               [
                                 {
                                   "titulo_grafico": "Descrição clara e executiva do gráfico",
                                   "tipo_grafico": "barras|linha|pizza",
                                   "indice_dimensao": <numero_do_indice_da_dimensao>,
                                   "indice_metrica": <numero_do_indice_da_metrica>
                                 }
                               ]
                               """;

            var response = await model.GenerateContent(promptText);
            var responseText = response?.Text?.Trim() ?? string.Empty;

            if (string.IsNullOrEmpty(responseText)) return null;

            // Extração defensiva com Regex para capturar o array JSON
            var matchJson = Regex.Match(responseText, @"\[\s*\{[\s\S]*\}\s*\]");
            if (!matchJson.Success)
            {
                return null;
            }

            var jsonPlanoAnalise = matchJson.Value;
            var opcoesJson = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var sugestoesBrutas = JsonSerializer.Deserialize<List<AnaliseSugerida>>(jsonPlanoAnalise, opcoesJson);

            if (sugestoesBrutas == null || sugestoesBrutas.Count == 0)
            {
                return null;
            }

            // Validação defensiva: integridade de índices e regras semânticas
            var totalColunas = headers.Length;
            var colunasPorIndice = perfilColunas.ToDictionary(p => p.Indice);

            var sugestoesValidadas = new List<AnaliseSugerida>();
            foreach (var analise in sugestoesBrutas)
            {
                // 1. Validar limites de índices
                if (analise.indice_dimensao < 0 || analise.indice_dimensao >= totalColunas ||
                    analise.indice_metrica < 0 || analise.indice_metrica >= totalColunas)
                {
                    continue;
                }

                // 2. Rejeitar identificador como métrica
                if (colunasPorIndice.TryGetValue(analise.indice_metrica, out var perfilMetrica) &&
                    perfilMetrica.TipoSemantico == TipoSemanticoColuna.TextoIdentificador)
                {
                    continue;
                }

                // 3. Fallback seguro para tipo de gráfico
                var tipoNormalizado = analise.tipo_grafico?.ToLowerInvariant()?.Trim();
                if (tipoNormalizado != "barras" && tipoNormalizado != "linha" && tipoNormalizado != "pizza")
                {
                    analise.tipo_grafico = "barras";
                }

                sugestoesValidadas.Add(analise);
            }

            return sugestoesValidadas.Count > 0 ? sugestoesValidadas : null;
        }

        private DateTime TentarConverterParaData(object valorCategoria)
        {
            if (DateTime.TryParse(Convert.ToString(valorCategoria), new CultureInfo("pt-BR"), DateTimeStyles.None, out DateTime dt))
            {
                return dt;
            }
            return DateTime.MinValue;
        }
    }
}
