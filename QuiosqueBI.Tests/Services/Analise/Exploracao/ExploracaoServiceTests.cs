using AwesomeAssertions;
using Microsoft.Extensions.Configuration;
using Moq;
using QuiosqueBI.API.Models;
using QuiosqueBI.API.Services.Analise.Exploracao;
using QuiosqueBI.API.Services.Analise.Exploracao.Profiling;
using QuiosqueBI.API.Services.Analise.Preparacao;
using Xunit;

namespace QuiosqueBI.Tests.Services.Analise.Exploracao
{
    public class ExploracaoServiceTests
    {
        private readonly Mock<IConfiguration> _configMock;
        private readonly Mock<IPreparacaoService> _preparacaoMock;
        private readonly Mock<IProfilingService> _profilingMock;
        private readonly ExploracaoService _sut;

        public ExploracaoServiceTests()
        {
            _configMock = new Mock<IConfiguration>();
            _preparacaoMock = new Mock<IPreparacaoService>();
            _profilingMock = new Mock<IProfilingService>();

            _sut = new ExploracaoService(
                _configMock.Object,
                _preparacaoMock.Object,
                _profilingMock.Object
            );
        }

        [Fact]
        public async Task GerarInsightsIniciaisAsync_SemChaveDeApi_DeveLancarInvalidOperationException()
        {
            // Arrange
            _configMock.Setup(c => c["Gemini:ApiKey"]).Returns((string?)null);
            _profilingMock.Setup(p => p.AnalisarColunas(It.IsAny<DadosArquivo.RDadosArquivo>()))
                .Returns(new List<PerfilColuna>());

            var dados = new DadosArquivo.RDadosArquivo(new[] { "Col1" }, new List<dynamic>());

            // Act
            var act = () => _sut.GerarInsightsIniciaisAsync(dados, "Objetivo de teste");

            // Assert
            await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("*Chave da API do Gemini não configurada*");
        }

        [Fact]
        public async Task GerarInsightsIniciaisAsync_DeveExecutarProfilingAntesDeChamarGemini()
        {
            // Arrange
            _configMock.Setup(c => c["Gemini:ApiKey"]).Returns((string?)null);
            var dados = new DadosArquivo.RDadosArquivo(new[] { "Col1" }, new List<dynamic>());

            // Act
            try
            {
                await _sut.GerarInsightsIniciaisAsync(dados, "Objetivo de teste");
            }
            catch (InvalidOperationException)
            {
                // Esperado, pois a API key é nula
            }

            // Assert
            _profilingMock.Verify(p => p.AnalisarColunas(dados), Times.Once);
        }

        [Fact]
        public void ProcessarERefinarPlanoAnalise_ComTextoConversacionalEmVolta_DeveExtrairJsonViaRegexComSucesso()
        {
            // Arrange
            var headers = new[] { "Categoria", "Valor" };
            var perfis = new List<PerfilColuna>
            {
                new(0, "Categoria", TipoSemanticoColuna.TextoCategoria, 5, 0.2, new List<string>(), false, true),
                new(1, "Valor", TipoSemanticoColuna.NumericoMonetario, 20, 1.0, new List<string>(), true, false)
            };

            var respostaConversacional = """
                Com certeza! Aqui estão as análises recomendadas para o seu negócio:
                ```json
                [
                  {
                    "titulo_grafico": "Vendas por Categoria",
                    "tipo_grafico": "barras",
                    "indice_dimensao": 0,
                    "indice_metrica": 1
                  }
                ]
                ```
                Espero ter ajudado na tomada de decisão!
                """;

            // Act
            var resultado = _sut.ProcessarERefinarPlanoAnalise(respostaConversacional, headers, perfis);

            // Assert
            resultado.Should().NotBeNull();
            resultado.Should().HaveCount(1);
            resultado![0].titulo_grafico.Should().Be("Vendas por Categoria");
            resultado[0].tipo_grafico.Should().Be("barras");
            resultado[0].indice_dimensao.Should().Be(0);
            resultado[0].indice_metrica.Should().Be(1);
        }

        [Fact]
        public void ProcessarERefinarPlanoAnalise_ComIndiceForaDosLimites_DeveDescartarSugestaoInvalida()
        {
            // Arrange
            var headers = new[] { "Categoria", "Valor" };
            var perfis = new List<PerfilColuna>
            {
                new(0, "Categoria", TipoSemanticoColuna.TextoCategoria, 5, 0.2, new List<string>(), false, true),
                new(1, "Valor", TipoSemanticoColuna.NumericoMonetario, 20, 1.0, new List<string>(), true, false)
            };

            var respostaComIndiceInvalido = """
                [
                  {
                    "titulo_grafico": "Análise Válida",
                    "tipo_grafico": "barras",
                    "indice_dimensao": 0,
                    "indice_metrica": 1
                  },
                  {
                    "titulo_grafico": "Análise com Índice Fora",
                    "tipo_grafico": "barras",
                    "indice_dimensao": 0,
                    "indice_metrica": 99
                  }
                ]
                """;

            // Act
            var resultado = _sut.ProcessarERefinarPlanoAnalise(respostaComIndiceInvalido, headers, perfis);

            // Assert
            resultado.Should().NotBeNull();
            resultado.Should().HaveCount(1);
            resultado![0].titulo_grafico.Should().Be("Análise Válida");
        }

        [Fact]
        public void ProcessarERefinarPlanoAnalise_ComColunaIdentificadorComoMetrica_DeveRejeitarSugestao()
        {
            // Arrange
            var headers = new[] { "Categoria", "CPF_Cliente" };
            var perfis = new List<PerfilColuna>
            {
                new(0, "Categoria", TipoSemanticoColuna.TextoCategoria, 5, 0.2, new List<string>(), false, true),
                new(1, "CPF_Cliente", TipoSemanticoColuna.TextoIdentificador, 20, 1.0, new List<string> { "[Anonimizado LGPD]" }, false, false)
            };

            var respostaComMetricaProibida = """
                [
                  {
                    "titulo_grafico": "Soma de CPFs (Alucinação)",
                    "tipo_grafico": "barras",
                    "indice_dimensao": 0,
                    "indice_metrica": 1
                  }
                ]
                """;

            // Act
            var resultado = _sut.ProcessarERefinarPlanoAnalise(respostaComMetricaProibida, headers, perfis);

            // Assert
            resultado.Should().BeNull();
        }

        [Fact]
        public void ProcessarERefinarPlanoAnalise_ComTipoGraficoInvalido_DeveAplicarFallbackParaBarras()
        {
            // Arrange
            var headers = new[] { "Categoria", "Valor" };
            var perfis = new List<PerfilColuna>
            {
                new(0, "Categoria", TipoSemanticoColuna.TextoCategoria, 5, 0.2, new List<string>(), false, true),
                new(1, "Valor", TipoSemanticoColuna.NumericoMonetario, 20, 1.0, new List<string>(), true, false)
            };

            var respostaComGraficoEstranho = """
                [
                  {
                    "titulo_grafico": "Gráfico Radar Desconhecido",
                    "tipo_grafico": "radar",
                    "indice_dimensao": 0,
                    "indice_metrica": 1
                  }
                ]
                """;

            // Act
            var resultado = _sut.ProcessarERefinarPlanoAnalise(respostaComGraficoEstranho, headers, perfis);

            // Assert
            resultado.Should().NotBeNull();
            resultado![0].tipo_grafico.Should().Be("barras");
        }
    }
}
