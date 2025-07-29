using CsvHelper;
using CsvHelper.Configuration;
using ExcelDataReader;
using QuiosqueBI.API.Models;
using System.Dynamic;
using System.Globalization;

namespace QuiosqueBI.API.Services.Analise.Fase1_Coleta
{
    public class ColetaService : IColetaService
    {
        public async Task<DadosArquivo.RDadosArquivo> LerDadosDoArquivoAsync(IFormFile arquivo)
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
                
                // Retorna os dados lidos. O ToList() é importante para materializar os dados
                // do stream antes que ele seja fechado.
                return new DadosArquivo.RDadosArquivo(headers, records.ToList());
            }
            else // Lógica para CSV
            {
                using var reader = new StreamReader(stream);
                // Sua lógica de leitura de CSV que funcionava.
                // Ajuste o delimitador se necessário.
                var config = new CsvConfiguration(new CultureInfo("pt-BR")) { Delimiter = ",", BadDataFound = null };
                using var csv = new CsvReader(reader, config);
                csv.Read();
                csv.ReadHeader();
                var headers = csv.HeaderRecord ?? Array.Empty<string>();
                var records = csv.GetRecords<dynamic>().ToList();
                return new DadosArquivo.RDadosArquivo(headers, records);
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
    }
}