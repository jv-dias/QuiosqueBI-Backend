using System.Dynamic;
using AwesomeAssertions;
using QuiosqueBI.API.Models;
using QuiosqueBI.API.Services.Analise.Preparacao;
using Xunit;

namespace QuiosqueBI.Tests.Services.Analise.Preparacao
{
    public class PreparacaoServiceTests
    {
        private readonly PreparacaoService _sut;

        public PreparacaoServiceTests()
        {
            _sut = new PreparacaoService();
        }

        [Theory]
        [InlineData("R$ 1.500,50", 1500.50)]
        [InlineData("1.500,50", 1500.50)]
        [InlineData("R$ 10,00", 10.00)]
        [InlineData("1500,00", 1500.00)]
        [InlineData("0,75", 0.75)]
        public void ConverterParaDecimal_ComFormatoBrasileiro_DeveConverterComSucesso(string valor, decimal esperado)
        {
            // Act
            var resultado = _sut.ConverterParaDecimal(valor);

            // Assert
            resultado.Should().Be(esperado);
        }

        [Theory]
        [InlineData("$1,500.50", 1500.50)]
        [InlineData("1,500.50", 1500.50)]
        [InlineData("$25.00", 25.00)]
        [InlineData("1000000.00", 1000000.00)]
        public void ConverterParaDecimal_ComFormatoAmericano_DeveConverterComSucesso(string valor, decimal esperado)
        {
            // Act
            var resultado = _sut.ConverterParaDecimal(valor);

            // Assert
            resultado.Should().Be(esperado);
        }

        [Theory]
        [InlineData("(1.500,50)", -1500.50)]
        [InlineData("($1,500.50)", -1500.50)]
        [InlineData("(50)", -50.0)]
        [InlineData("(0,99)", -0.99)]
        public void ConverterParaDecimal_ComNotacaoContabilParenteses_DeveRetornarValorNegativo(string valor, decimal esperado)
        {
            // Act
            var resultado = _sut.ConverterParaDecimal(valor);

            // Assert
            resultado.Should().Be(esperado);
        }

        [Theory]
        [InlineData("-R$ 50,25", -50.25)]
        [InlineData("-150.00", -150.00)]
        [InlineData("-1.000,00", -1000.00)]
        public void ConverterParaDecimal_ComSinalNegativoExplicito_DeveRetornarValorNegativo(string valor, decimal esperado)
        {
            // Act
            var resultado = _sut.ConverterParaDecimal(valor);

            // Assert
            resultado.Should().Be(esperado);
        }

        [Theory]
        [InlineData("15%", 15.0)]
        [InlineData("15,5%", 15.5)]
        [InlineData("0.5%", 0.5)]
        public void ConverterParaDecimal_ComPercentual_DeveRetornarValorNumerico(string valor, decimal esperado)
        {
            // Act
            var resultado = _sut.ConverterParaDecimal(valor);

            // Assert
            resultado.Should().Be(esperado);
        }

        [Fact]
        public void ConverterParaDecimal_ComTiposNumericosNativos_DeveConverterDiretamente()
        {
            // Arrange & Act
            var resultadoDouble = _sut.ConverterParaDecimal(1234.56);
            var resultadoInt = _sut.ConverterParaDecimal(100);
            var resultadoLong = _sut.ConverterParaDecimal(5000L);
            var resultadoFloat = _sut.ConverterParaDecimal(12.5f);

            // Assert
            resultadoDouble.Should().Be(1234.56m);
            resultadoInt.Should().Be(100m);
            resultadoLong.Should().Be(5000m);
            resultadoFloat.Should().Be(12.5m);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("Não Identificado")]
        [InlineData("NÃO IDENTIFICADO")]
        [InlineData("texto_aleatorio")]
        public void ConverterParaDecimal_ComValoresNulosVaziosOuInvalidos_DeveRetornarZero(object? valor)
        {
            // Act
            var resultado = _sut.ConverterParaDecimal(valor);

            // Assert
            resultado.Should().Be(0m);
        }

        [Fact]
        public void PrepararDados_ComLinhasTotalmenteVazias_DeveDescartarApenasAsVazias()
        {
            // Arrange
            var headers = new[] { "Produto", "Valor" };
            var rows = new List<dynamic>();

            // Linha 1: Válida
            dynamic r1 = new ExpandoObject();
            var d1 = (IDictionary<string, object>)r1;
            d1["Produto"] = "Café";
            d1["Valor"] = "10.00";
            rows.Add(r1);

            // Linha 2: 100% Vazia (resíduo de planilha)
            dynamic r2 = new ExpandoObject();
            var d2 = (IDictionary<string, object>)r2;
            d2["Produto"] = "   ";
            d2["Valor"] = null!;
            rows.Add(r2);

            // Linha 3: Parcial
            dynamic r3 = new ExpandoObject();
            var d3 = (IDictionary<string, object>)r3;
            d3["Produto"] = "Bolo";
            d3["Valor"] = null!;
            rows.Add(r3);

            var dadosBrutos = new DadosArquivo.RDadosArquivo(headers, rows);

            // Act
            var dadosSanitizados = _sut.PrepararDados(dadosBrutos);

            // Assert
            dadosSanitizados.Records.Should().HaveCount(2);
            var registros = dadosSanitizados.Records.Cast<IDictionary<string, object>>().ToList();
            registros[0]["Produto"].Should().Be("Café");
            registros[1]["Produto"].Should().Be("Bolo");
            registros[1]["Valor"].Should().Be("Não Identificado");
        }

        [Fact]
        public void PrepararDados_ComCabecalhosIrregularesEDuplicados_DeveSanitizarENomearUnicamente()
        {
            // Arrange
            var headers = new[] { "  Produto  ", "Preço\r\nUnitário", "", "Preço\nUnitário" };
            dynamic r1 = new ExpandoObject();
            var d1 = (IDictionary<string, object>)r1;
            d1["  Produto  "] = "Livro";
            d1["Preço\r\nUnitário"] = "50.00";
            d1[""] = "Info";
            d1["Preço\nUnitário"] = "45.00";

            var dadosBrutos = new DadosArquivo.RDadosArquivo(headers, new List<dynamic> { r1 });

            // Act
            var resultado = _sut.PrepararDados(dadosBrutos);

            // Assert
            resultado.Headers.Should().HaveCount(4);
            resultado.Headers[0].Should().Be("Produto");
            resultado.Headers[1].Should().Be("Preço Unitário");
            resultado.Headers[2].Should().Be("Coluna_3");
            resultado.Headers[3].Should().Be("Preço Unitário_2");
        }
    }
}
