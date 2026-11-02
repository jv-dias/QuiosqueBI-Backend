using AwesomeAssertions;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Http;
using Moq;
using QuiosqueBI.API.Services.Analise.Coleta;
using System.Text;

namespace QuiosqueBI.Tests.Services.Analise.Coleta
{
    public class ColetaServiceTests
    {
        private readonly ColetaService _service;

        public ColetaServiceTests()
        {
            _service = new ColetaService();
        }

        private static IFormFile CriarFormFileMock(byte[] conteudo, string nomeArquivo)
        {
            var mock = new Mock<IFormFile>();
            mock.Setup(f => f.OpenReadStream()).Returns(() => new MemoryStream(conteudo));
            mock.Setup(f => f.FileName).Returns(nomeArquivo);
            mock.Setup(f => f.Length).Returns(conteudo.Length);
            return mock.Object;
        }

        private static IFormFile CriarFormFileCsv(string conteudoCsv, string nomeArquivo = "dados.csv")
        {
            var bytes = Encoding.UTF8.GetBytes(conteudoCsv);
            return CriarFormFileMock(bytes, nomeArquivo);
        }

        [Fact]
        public async Task LerDadosDoArquivoAsync_CsvValido_DeveRetornarCabecalhosERegistrosCorretos()
        {
            // Arrange
            var csv = "Data,Produto,Valor\n" +
                      "2025-01-01,Pastel de Queijo,12.50\n" +
                      "2025-01-02,Caldo de Cana,8.00\n";
            var arquivo = CriarFormFileCsv(csv);

            // Act
            var resultado = await _service.LerDadosDoArquivoAsync(arquivo);

            // Assert
            resultado.Headers.Should().BeEquivalentTo(new[] { "Data", "Produto", "Valor" });
            var records = resultado.Records.ToList();
            records.Should().HaveCount(2);

            var primeiroRegistro = (IDictionary<string, object>)records[0];
            primeiroRegistro["Data"].Should().Be("2025-01-01");
            primeiroRegistro["Produto"].Should().Be("Pastel de Queijo");
            primeiroRegistro["Valor"].Should().Be("12.50");

            var segundoRegistro = (IDictionary<string, object>)records[1];
            segundoRegistro["Data"].Should().Be("2025-01-02");
            segundoRegistro["Produto"].Should().Be("Caldo de Cana");
            segundoRegistro["Valor"].Should().Be("8.00");
        }

        [Fact]
        public async Task LerDadosDoArquivoAsync_CsvApenasCabecalho_DeveRetornarCabecalhosERegistrosVazios()
        {
            // Arrange
            var csv = "ID,Cliente,Telefone\n";
            var arquivo = CriarFormFileCsv(csv);

            // Act
            var resultado = await _service.LerDadosDoArquivoAsync(arquivo);

            // Assert
            resultado.Headers.Should().BeEquivalentTo(new[] { "ID", "Cliente", "Telefone" });
            resultado.Records.Should().BeEmpty();
        }

        [Fact]
        public async Task LerDadosDoArquivoAsync_CsvComVirgulaEntreAspas_DeveManterCampoUnico()
        {
            // Arrange
            var csv = "Produto,Preco\n" +
                      "\"Café, Extra Forte\",15.00\n";
            var arquivo = CriarFormFileCsv(csv);

            // Act
            var resultado = await _service.LerDadosDoArquivoAsync(arquivo);

            // Assert
            resultado.Headers.Should().BeEquivalentTo(new[] { "Produto", "Preco" });
            var records = resultado.Records.ToList();
            records.Should().HaveCount(1);

            var registro = (IDictionary<string, object>)records[0];
            registro["Produto"].Should().Be("Café, Extra Forte");
            registro["Preco"].Should().Be("15.00");
        }

        [Fact]
        public async Task LerDadosDoArquivoAsync_CsvComQuebrasCrlf_DeveProcessarCorretamente()
        {
            // Arrange
            var csv = "Vendedor,Total\r\nCarlos,1200\r\nAna,3500\r\n";
            var arquivo = CriarFormFileCsv(csv);

            // Act
            var resultado = await _service.LerDadosDoArquivoAsync(arquivo);

            // Assert
            resultado.Headers.Should().BeEquivalentTo(new[] { "Vendedor", "Total" });
            var records = resultado.Records.ToList();
            records.Should().HaveCount(2);

            var primeiro = (IDictionary<string, object>)records[0];
            primeiro["Vendedor"].Should().Be("Carlos");
            primeiro["Total"].Should().Be("1200");
        }

        [Fact]
        public async Task LerDadosDoArquivoAsync_XlsxValido_DeveRetornarCabecalhosERegistrosCorretos()
        {
            // Arrange
            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("Vendas");
            worksheet.Cell(1, 1).Value = "Vendedor";
            worksheet.Cell(1, 2).Value = "Regiao";
            worksheet.Cell(1, 3).Value = "Vendas";

            worksheet.Cell(2, 1).Value = "Joao";
            worksheet.Cell(2, 2).Value = "Sul";
            worksheet.Cell(2, 3).Value = 1500;

            worksheet.Cell(3, 1).Value = "Maria";
            worksheet.Cell(3, 2).Value = "Norte";
            worksheet.Cell(3, 3).Value = 2300;

            using var ms = new MemoryStream();
            workbook.SaveAs(ms);
            var arquivo = CriarFormFileMock(ms.ToArray(), "relatorio.xlsx");

            // Act
            var resultado = await _service.LerDadosDoArquivoAsync(arquivo);

            // Assert
            resultado.Headers.Should().BeEquivalentTo(new[] { "Vendedor", "Regiao", "Vendas" });
            var records = resultado.Records.ToList();
            records.Should().HaveCount(2);

            var primeiro = (IDictionary<string, object>)records[0];
            primeiro["Vendedor"].ToString().Should().Be("Joao");
            primeiro["Regiao"].ToString().Should().Be("Sul");
            Convert.ToDouble(primeiro["Vendas"]).Should().Be(1500);

            var segundo = (IDictionary<string, object>)records[1];
            segundo["Vendedor"].ToString().Should().Be("Maria");
            segundo["Regiao"].ToString().Should().Be("Norte");
            Convert.ToDouble(segundo["Vendas"]).Should().Be(2300);
        }

        [Fact]
        public async Task LerDadosDoArquivoAsync_XlsxComValoresNulos_DevePreservarChavesComValorNulo()
        {
            // Arrange
            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("DadosIncompletos");
            worksheet.Cell(1, 1).Value = "Codigo";
            worksheet.Cell(1, 2).Value = "Observacao";

            worksheet.Cell(2, 1).Value = "A100";
            // Observação vazia na linha 2

            using var ms = new MemoryStream();
            workbook.SaveAs(ms);
            var arquivo = CriarFormFileMock(ms.ToArray(), "incompletos.xlsx");

            // Act
            var resultado = await _service.LerDadosDoArquivoAsync(arquivo);

            // Assert
            resultado.Headers.Should().BeEquivalentTo(new[] { "Codigo", "Observacao" });
            var records = resultado.Records.ToList();
            records.Should().HaveCount(1);

            var registro = (IDictionary<string, object>)records[0];
            registro["Codigo"].ToString().Should().Be("A100");
            registro["Observacao"].Should().BeNull();
        }
    }
}
