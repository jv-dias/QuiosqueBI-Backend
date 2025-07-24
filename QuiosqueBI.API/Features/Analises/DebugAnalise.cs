using MediatR;
using Microsoft.AspNetCore.Mvc;
using QuiosqueBI.API.Models;
using CsvHelper;
using CsvHelper.Configuration;
using ExcelDataReader;
using Mscc.GenerativeAI;
using System.Dynamic;
using System.Globalization;
using System.Text.Json;

namespace QuiosqueBI.API.Features.Analises;

public static class DebugAnalise
{
    public record Query(IFormFile Arquivo, string Contexto) : IRequest<IActionResult>;

    public class Handler : IRequestHandler<Query, IActionResult>
    {
        private readonly IConfiguration _configuration;

        public Handler(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task<IActionResult> Handle(Query request, CancellationToken cancellationToken)
        {
            if (request.Arquivo == null || request.Arquivo.Length == 0)
            {
                return new BadRequestObjectResult("Nenhum arquivo enviado.");
            }

            try
            {
                var debugData = await GerarDadosDebugAsync(request.Arquivo, request.Contexto);
                return new OkObjectResult(debugData);
            }
            catch (Exception ex)
            {
                return new ObjectResult($"Ocorreu um erro interno na rota de depuração: {ex.Message}")
                {
                    StatusCode = 500
                };
            }
        }

        private async Task<DebugData> GerarDadosDebugAsync(IFormFile arquivo, string contexto)
        {
            var processador = async (DadosArquivo.RDadosArquivo dadosArquivo) =>
            {
                var planoDeAnalise = await ObterPlanoDeAnaliseAsync(dadosArquivo.Headers, contexto);
                return new DebugData
                {
                    CabecalhosDoArquivo = dadosArquivo.Headers,
                    DadosBrutosDoArquivo = dadosArquivo.Records.Take(10).ToList(),
                    PlanoDaIA = planoDeAnalise
                };
            };

            return await ProcessarArquivoStreamAsync(arquivo, processador);
        }

        private async Task<T> ProcessarArquivoStreamAsync<T>(IFormFile arquivo, Func<DadosArquivo.RDadosArquivo, Task<T>> processador)
        {
            await using var stream = arquivo.OpenReadStream();
            var extensao = Path.GetExtension(arquivo.FileName).ToLowerInvariant();

            if (extensao == ".xlsx")
            {
                System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

                using var reader = ExcelReaderFactory.CreateReader(stream);

                reader.Read();
                var headerList = new List<string>();
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    headerList.Add(reader.GetValue(i)?.ToString() ?? string.Empty);
                }
                var headers = headerList.ToArray();

                var records = LerLinhasExcel(reader, headers);

                return await processador(new DadosArquivo.RDadosArquivo(headers, records));
            }
            else
            {
                using var reader = new StreamReader(stream);
                var linhas = new List<string>();
                while (!reader.EndOfStream)
                {
                    var linha = await reader.ReadLineAsync() ?? "";
                    if (linha.StartsWith("\"") && linha.EndsWith("\""))
                        linha = linha.Substring(1, linha.Length - 2);
                    linhas.Add(linha);
                }
                using var stringReader = new StringReader(string.Join('\n', linhas));
                var config = new CsvConfiguration(new CultureInfo("pt-BR"))
                {
                    Delimiter = ",", BadDataFound = null
                };
                using var csv = new CsvReader(stringReader, config);

                csv.Read();
                csv.ReadHeader();
                var headers = csv.HeaderRecord ?? Array.Empty<string>();
                var records = csv.GetRecords<dynamic>();
                return await processador(new DadosArquivo.RDadosArquivo(headers, records));
            }
        }

        private IEnumerable<dynamic> LerLinhasExcel(IExcelDataReader reader, string[] headers)
        {
            while (reader.Read())
            {
                var expando = new ExpandoObject() as IDictionary<string, object>;
                for (int i = 0; i < headers.Length; i++)
                {
                    expando[headers[i]] = reader.GetValue(i);
                }
                yield return expando;
            }
        }

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
                               Com base no objetivo e nas colunas, sugira até 5 análises relevantes de dimensão e métrica. Para cada sugestão, forneça o índice numérico da coluna de dimensão e o índice numérico da coluna de métrica.
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
    }
}
