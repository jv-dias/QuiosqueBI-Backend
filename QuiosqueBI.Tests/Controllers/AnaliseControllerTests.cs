using AwesomeAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using QuiosqueBI.API.Controllers;
using QuiosqueBI.API.Models;
using QuiosqueBI.API.Services.Analise;
using System.Security.Claims;

namespace QuiosqueBI.Tests.Controllers
{
    public class AnaliseControllerTests
    {
        private static ClaimsPrincipal CriarClaimsPrincipalUsuario(string userId = "user-guid-123")
        {
            return new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, userId),
                new Claim(ClaimTypes.Name, "Usuario-Teste")
            }, "TestAuthType"));
        }

        private static AnaliseController CriarControllerComUsuario(
            Mock<IAnaliseOrquestradorService> orquestradorMock,
            ClaimsPrincipal? user = null)
        {
            var usuario = user ?? CriarClaimsPrincipalUsuario();
            var controller = new AnaliseController(orquestradorMock.Object)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext { User = usuario }
                }
            };
            return controller;
        }

        [Fact]
        public async Task Upload_Sucesso_DeveRetornarOk200ComResultados()
        {
            // Arrange
            var orquestradorMock = new Mock<IAnaliseOrquestradorService>();
            var arquivoMock = new Mock<IFormFile>();
            var contexto = "Análise de vendas";

            var listaResultados = new List<ResultadoGrafico>
            {
                new ResultadoGrafico
                {
                    Titulo = "Vendas por Categoria",
                    TipoGrafico = "pie",
                    Dados = new List<object>()
                }
            };

            orquestradorMock.Setup(o => o.ExecutarAnaliseCompletaAsync(
                arquivoMock.Object, contexto, It.IsAny<ClaimsPrincipal>()))
                .ReturnsAsync(listaResultados);

            var controller = CriarControllerComUsuario(orquestradorMock);

            // Act
            var resultado = await controller.Upload(arquivoMock.Object, contexto);

            // Assert
            var okResult = resultado.Should().BeOfType<OkObjectResult>().Subject;
            okResult.StatusCode.Should().Be(200);

            var valor = okResult.Value;
            valor.Should().NotBeNull();
            var propResultados = valor!.GetType().GetProperty("Resultados");
            propResultados.Should().NotBeNull();
            var resultadosObtidos = propResultados!.GetValue(valor) as List<ResultadoGrafico>;
            resultadosObtidos.Should().NotBeNull();
            resultadosObtidos.Should().HaveCount(1);
            resultadosObtidos![0].Titulo.Should().Be("Vendas por Categoria");
        }

        [Fact]
        public async Task Upload_ExcecaoInterna_DeveRetornarStatus500ComMensagemDeErro()
        {
            // Arrange
            var orquestradorMock = new Mock<IAnaliseOrquestradorService>();
            var arquivoMock = new Mock<IFormFile>();

            orquestradorMock.Setup(o => o.ExecutarAnaliseCompletaAsync(
                It.IsAny<IFormFile>(), It.IsAny<string>(), It.IsAny<ClaimsPrincipal>()))
                .ThrowsAsync(new InvalidOperationException("Falha na API da IA Gemini"));

            var controller = CriarControllerComUsuario(orquestradorMock);

            // Act
            var resultado = await controller.Upload(arquivoMock.Object, "Contexto");

            // Assert
            var statusResult = resultado.Should().BeOfType<ObjectResult>().Subject;
            statusResult.StatusCode.Should().Be(500);
            statusResult.Value.Should().Be("Ocorreu um erro interno: Falha na API da IA Gemini");
        }

        [Fact]
        public async Task ListarHistorico_Sucesso_DeveRetornarOk200ComHistorico()
        {
            // Arrange
            var orquestradorMock = new Mock<IAnaliseOrquestradorService>();
            var historicoEsperado = new List<AnaliseSalva>
            {
                new AnaliseSalva { Id = 1, UserId = "user-1", Contexto = "Análise 1", DataCriacao = DateTime.UtcNow },
                new AnaliseSalva { Id = 2, UserId = "user-1", Contexto = "Análise 2", DataCriacao = DateTime.UtcNow }
            };

            orquestradorMock.Setup(o => o.ListarHistoricoAsync(It.IsAny<ClaimsPrincipal>()))
                .ReturnsAsync(historicoEsperado);

            var controller = CriarControllerComUsuario(orquestradorMock);

            // Act
            var resultado = await controller.ListarHistorico();

            // Assert
            var okResult = resultado.Should().BeOfType<OkObjectResult>().Subject;
            okResult.StatusCode.Should().Be(200);

            var listaRetornada = okResult.Value.Should().BeAssignableTo<List<AnaliseSalva>>().Subject;
            listaRetornada.Should().HaveCount(2);
            listaRetornada[0].Contexto.Should().Be("Análise 1");
        }

        [Fact]
        public async Task ListarHistorico_ExcecaoInterna_DeveRetornarStatus500()
        {
            // Arrange
            var orquestradorMock = new Mock<IAnaliseOrquestradorService>();
            orquestradorMock.Setup(o => o.ListarHistoricoAsync(It.IsAny<ClaimsPrincipal>()))
                .ThrowsAsync(new Exception("Falha de conexão com PostgreSQL"));

            var controller = CriarControllerComUsuario(orquestradorMock);

            // Act
            var resultado = await controller.ListarHistorico();

            // Assert
            var statusResult = resultado.Should().BeOfType<ObjectResult>().Subject;
            statusResult.StatusCode.Should().Be(500);
            statusResult.Value.Should().Be("Ocorreu um erro interno: Falha de conexão com PostgreSQL");
        }

        [Fact]
        public async Task ObterAnalisePorId_AnaliseExistente_DeveRetornarOk200ComAnalise()
        {
            // Arrange
            var orquestradorMock = new Mock<IAnaliseOrquestradorService>();
            var analiseEsperada = new AnaliseSalva
            {
                Id = 42,
                UserId = "user-1",
                Contexto = "Relatório de Q3",
                ResultadosJson = "{}"
            };

            orquestradorMock.Setup(o => o.ObterAnalisePorIdAsync(42, It.IsAny<ClaimsPrincipal>()))
                .ReturnsAsync(analiseEsperada);

            var controller = CriarControllerComUsuario(orquestradorMock);

            // Act
            var resultado = await controller.ObterAnalisePorId(42);

            // Assert
            var okResult = resultado.Should().BeOfType<OkObjectResult>().Subject;
            okResult.StatusCode.Should().Be(200);

            var analiseRetornada = okResult.Value.Should().BeAssignableTo<AnaliseSalva>().Subject;
            analiseRetornada.Id.Should().Be(42);
            analiseRetornada.Contexto.Should().Be("Relatório de Q3");
        }

        [Fact]
        public async Task ObterAnalisePorId_AnaliseInexistenteOuDeOutroUsuario_DeveRetornarNotFound404()
        {
            // Arrange
            var orquestradorMock = new Mock<IAnaliseOrquestradorService>();
            orquestradorMock.Setup(o => o.ObterAnalisePorIdAsync(999, It.IsAny<ClaimsPrincipal>()))
                .ReturnsAsync((AnaliseSalva?)null);

            var controller = CriarControllerComUsuario(orquestradorMock);

            // Act
            var resultado = await controller.ObterAnalisePorId(999);

            // Assert
            var notFound = resultado.Should().BeOfType<NotFoundResult>().Subject;
            notFound.StatusCode.Should().Be(404);
        }

        [Fact]
        public async Task ObterAnalisePorId_ExcecaoInterna_DeveRetornarStatus500()
        {
            // Arrange
            var orquestradorMock = new Mock<IAnaliseOrquestradorService>();
            orquestradorMock.Setup(o => o.ObterAnalisePorIdAsync(42, It.IsAny<ClaimsPrincipal>()))
                .ThrowsAsync(new Exception("Erro de I/O"));

            var controller = CriarControllerComUsuario(orquestradorMock);

            // Act
            var resultado = await controller.ObterAnalisePorId(42);

            // Assert
            var statusResult = resultado.Should().BeOfType<ObjectResult>().Subject;
            statusResult.StatusCode.Should().Be(500);
            statusResult.Value.Should().Be("Ocorreu um erro interno: Erro de I/O");
        }
    }
}
