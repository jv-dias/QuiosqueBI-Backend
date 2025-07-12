using CsvHelper;
using CsvHelper.Configuration;
using ExcelDataReader;
using Microsoft.AspNetCore.Http;
using Mscc.GenerativeAI;
using QuiosqueBI.API.Models;
using System.Dynamic;
using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using QuiosqueBI.API.Data;

namespace QuiosqueBI.API.Services
{
    public class AnaliseService : IAnaliseService
    {
        private readonly IConfiguration _configuration;
        private readonly ApplicationDbContext _context;

        public AnaliseService(IConfiguration configuration, ApplicationDbContext context)
        {
            _configuration = configuration;
            _context = context;
        }

        // MÉTODO PÚBLICO PRINCIPAL
        public async Task<List<ResultadoGrafico>> GerarResultadosAnaliseAsync(IFormFile arquivo, string contexto, string userId)
        {
            // O "processador" é uma função anônima (lambda) que contém a lógica de negócio.
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

                    // Salvar os resultados no banco de dados
                    await SalvarResultadosNoBancoAsync(resultadosFinais, contexto, userId);
                }
                return resultadosFinais;
            };

            return await ProcessarArquivoStreamAsync(arquivo, processador);
        }

        // Método auxiliar para salvar resultados no banco
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
                await SalvarAnalise(analiseSalva);
            }
        }

        // MÉTODO PÚBLICO DE DEBUG
        public async Task<DebugData> GerarDadosDebugAsync(IFormFile arquivo, string contexto)
        {
            var processador = async (DadosArquivo.RDadosArquivo dadosArquivo) =>
            {
                var planoDeAnalise = await ObterPlanoDeAnaliseAsync(dadosArquivo.Headers, contexto);
                return new DebugData
                {
                    CabecalhosDoArquivo = dadosArquivo.Headers,
                    DadosBrutosDoArquivo = dadosArquivo.Records.Take(10).ToList(), // Pega só os 10 primeiros
                    PlanoDaIA = planoDeAnalise
                };
            };

            return await ProcessarArquivoStreamAsync(arquivo, processador);
        }

        // ===================================================================
        // NOVO MÉTODO CENTRAL PARA LEITURA EM STREAMING
        // ===================================================================
        private async Task<T> ProcessarArquivoStreamAsync<T>(IFormFile arquivo, Func<DadosArquivo.RDadosArquivo, Task<T>> processador)
        {
            // Abre o stream do arquivo que será usado por qualquer um dos leitores
            await using var stream = arquivo.OpenReadStream();
            var extensao = Path.GetExtension(arquivo.FileName).ToLowerInvariant();

            if (extensao == ".xlsx")
            {
                // Garante o suporte a codificações de planilhas antigas
                System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

                // O bloco 'using' garante que o leitor permanecerá aberto durante o processamento
                using var reader = ExcelReaderFactory.CreateReader(stream);

                reader.Read(); // Lê a primeira linha para obter os cabeçalhos
                var headerList = new List<string>();
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    headerList.Add(reader.GetValue(i)?.ToString() ?? string.Empty);
                }
                var headers = headerList.ToArray();

                // Cria o IEnumerable "preguiçoso" para ler as linhas sob demanda
                var records = LerLinhasExcel(reader, headers);

                // *** PONTO CRÍTICO DA CORREÇÃO ***
                // Executa o processador AQUI DENTRO do bloco 'using', garantindo que o 'reader' está vivo.
                return await processador(new DadosArquivo.RDadosArquivo(headers, records));
            }
            else // Somente para CSV
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
                yield return expando; // Retorna uma linha de cada vez (streaming)
            }
        }

        // ... (Seus outros métodos privados: ObterPlanoDeAnaliseAsync, ConverterStringParaDecimal, TentarConverterParaData)
        // Eles permanecem os mesmos.
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

        public async Task<List<AnaliseSalva>> ListarAnalisesSalvasAsync(string userId)
        {
            // Adiciona a cláusula .Where() para filtrar apenas as análises do usuário logado
            return await _context.AnalisesSalvas
                .Where(a => a.UserId == userId)
                .OrderByDescending(a => a.DataCriacao)
                .ToListAsync();
        }

        public async Task SalvarAnalise(AnaliseSalva analise)
        {
            _context.AnalisesSalvas.Add(analise);
            await _context.SaveChangesAsync();
        }

        public async Task<AnaliseSalva?> ObterAnaliseSalvaPorIdAsync(int id, string userId)
        {
            // Busca pelo Id da análise E verifica se o UserId corresponde ao do usuário logado
            return await _context.AnalisesSalvas
                .FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId);
        }
    }
}