using MediatR;
using Microsoft.AspNetCore.Mvc;
using QuiosqueBI.API.Models;
using QuiosqueBI.API.Data;
using CsvHelper;
using CsvHelper.Configuration;
using ExcelDataReader;
using Mscc.GenerativeAI;
using System.Dynamic;
using System.Globalization;
using System.Text.Json;
using System.Security.Claims;

namespace QuiosqueBI.API.Features.Analises;

public static class UploadAnalise
{
    public record Command(IFormFile Arquivo, string Contexto, ClaimsPrincipal User) : IRequest<IActionResult>;

    public class Handler : IRequestHandler<Command, IActionResult>
    {
        private readonly IConfiguration _configuration;
        private readonly ApplicationDbContext _context;

        public Handler(IConfiguration configuration, ApplicationDbContext context)
        {
            _configuration = configuration;
            _context = context;
        }

        public async Task<IActionResult> Handle(Command request, CancellationToken cancellationToken)
        {
            var userId = request.User.FindFirstValue(ClaimTypes.NameIdentifier);
            
            if (userId == null)
            {
                return new UnauthorizedObjectResult("Usuário não autenticado.");
            }

            try
            {
                var resultadosFinais = await GerarResultadosAnaliseAsync(request.Arquivo, request.Contexto, userId);
                return new OkObjectResult(new { Resultados = resultadosFinais });
            }
            catch (Exception ex)
            {
                return new ObjectResult($"Ocorreu um erro interno: {ex.Message}")
                {
                    StatusCode = 500
                };
            }
        }

        private async Task<List<ResultadoGrafico>> GerarResultadosAnaliseAsync(IFormFile arquivo, string contexto, string userId)
        {
            var processador = async (DadosArquivo.RDadosArquivo dadosArquivo) =>
            {
                var planoDeAnalise = await ObterPlanoDeAnaliseAsync(dadosArquivo.Headers, contexto);
                var resultadosFinais = new List<ResultadoGrafico>();

                if (planoDeAnalise != null)
                {
                    var recordsList = dadosArquivo.Records.ToList();

                    foreach (var analise in planoDeAnalise)
                    {
                        if (analise.indice_dimensao < 0 || analise.indice_dimensao >= dadosArquivo.Headers.Length ||
                            analise.indice_metrica < 0 || analise.indice_metrica >= dadosArquivo.Headers.Length)
                        {
                            continue;
                        }

                        var cabecalhoDimensaoReal = dadosArquivo.Headers[analise.indice_dimensao];
                        var cabecalhoMetricaReal = dadosArquivo.Headers[analise.indice_metrica];

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
                            dadosAgrupados = dadosAgrupados
                                .OrderBy(x => TentarConverterParaData(x.Categoria))
                                .Select(x => new
                                {
                                    Categoria = x.Categoria == null ? null :
                                        (TentarConverterParaData(x.Categoria) != DateTime.MinValue
                                            ? (object)TentarConverterParaData(x.Categoria).ToString("yyyy-MM-dd")
                                            : x.Categoria),
                                    Valor = x.Valor
                                })
                                .ToList();
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

                    await SalvarResultadosNoBancoAsync(resultadosFinais, contexto, userId);
                }
                return resultadosFinais;
            };

            return await ProcessarArquivoStreamAsync(arquivo, processador);
        }

        private async Task SalvarResultadosNoBancoAsync(List<ResultadoGrafico> resultados, string contexto, string userId)
        {
            if (resultados.Any())
            {
                var analiseSalva = new AnaliseSalva
                {
                    Contexto = contexto,
                    DataCriacao = DateTime.UtcNow,
                    ResultadosJson = JsonSerializer.Serialize(resultados),
                    UserId = userId
                };
                
                _context.AnalisesSalvas.Add(analiseSalva);
                await _context.SaveChangesAsync();
            }
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
