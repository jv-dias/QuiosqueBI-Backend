using Mscc.GenerativeAI;
using QuiosqueBI.API.Models;
using System.Globalization;
using System.Text.Json;

namespace QuiosqueBI.API.Services.Analise.Fase3_Exploracao
{
    public class ExploracaoService : IExploracaoService
    {
        private readonly IConfiguration _configuration;

        public ExploracaoService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task<List<ResultadoGrafico>> GerarInsightsIniciaisAsync(DadosArquivo.RDadosArquivo dadosDoArquivo, string contexto)
        {
            var planoDeAnalise = await ObterPlanoDeAnaliseAsync(dadosDoArquivo.Headers, contexto);
            var resultadosFinais = new List<ResultadoGrafico>();

            if (planoDeAnalise != null)
            {
                var recordsList = dadosDoArquivo.Records.ToList();
                foreach (var analise in planoDeAnalise)
                {
                    // ... (Toda a sua lógica de GroupBy, Sum, Ordenação e formatação de datas
                    // que estava no seu AnaliseService antigo vai aqui) ...
                    var cabecalhoDimensaoReal = dadosDoArquivo.Headers[analise.indice_dimensao];
                    var cabecalhoMetricaReal = dadosDoArquivo.Headers[analise.indice_metrica];

                    var dadosAgrupados = recordsList
                        .AsParallel()
                        .GroupBy(rec => (object)((IDictionary<string, object>)rec)[cabecalhoDimensaoReal])
                        .Select(g => new
                        {
                            Categoria = g.Key,
                            Valor = g.Sum(rec => ConverterStringParaDecimal(Convert.ToString(((IDictionary<string, object>)rec)[cabecalhoMetricaReal])))
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

        // Os métodos privados que dão suporte à análise também são movidos para cá
        private async Task<List<AnaliseSugerida>?> ObterPlanoDeAnaliseAsync(string[] headers, string contexto)
        {
            var apiKey = _configuration["Gemini:ApiKey"] ?? throw new InvalidOperationException("Chave da API do Gemini não configurada.");

            var googleAi = new GoogleAI(apiKey);
            var model = googleAi.GenerativeModel(Model.Gemini25Flash);

            var colunasComIndices = string.Join(", ", headers.Select((h, i) => $"'{i}': '{h}'"));

            var promptText = $$"""
                               Sua tarefa é agir como um motor de análise de dados. Você receberá um objetivo e uma lista de colunas com seus respectivos índices numéricos. Sua resposta DEVE usar os índices.
                               O objetivo da análise do usuário é: '{{contexto}}'.
                               A lista de colunas e seus índices disponíveis é: {{{colunasComIndices}}}.
                               Com base no objetivo e nas colunas, sugira até 10 análises relevantes de dimensão e métrica. Para cada sugestão, forneça o índice numérico da coluna de dimensão e o índice numérico da coluna de métrica.
                               Responda APENAS com um objeto JSON válido no formato:
                               [
                                 { "titulo_grafico": "...", "tipo_grafico": "barras|linha|pizza", "indice_dimensao": <numero>, "indice_metrica": <numero> }
                               ]
                               """;

            var response = await model.GenerateContent(promptText);
            var responseText = response?.Text?.Trim() ?? string.Empty;

            if (string.IsNullOrEmpty(responseText)) return null;

            var jsonPlanoAnalise = responseText.Trim('`').Replace("json", "").Trim();
            var opcoesJson = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

            return JsonSerializer.Deserialize<List<AnaliseSugerida>>(jsonPlanoAnalise, opcoesJson);
        }
        
        private decimal ConverterStringParaDecimal(string? stringValue)
        {
            if (string.IsNullOrEmpty(stringValue)) return 0m;

            string cleanString = stringValue.Replace("R$", "").Trim().Replace(",", ".");

            if (decimal.TryParse(cleanString, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal parsedValue))
            {
                return parsedValue;
            }
            return 0m;
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