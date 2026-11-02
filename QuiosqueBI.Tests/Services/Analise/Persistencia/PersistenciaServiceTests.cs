using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using QuiosqueBI.API.Data;
using QuiosqueBI.API.Models;
using QuiosqueBI.API.Services.Analise.Persistencia;

namespace QuiosqueBI.Tests.Services.Analise.Persistencia
{
    public class PersistenciaServiceTests
    {
        private static ApplicationDbContext CriarDbContextEmMemoria()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            return new ApplicationDbContext(options);
        }

        [Fact]
        public async Task SalvarAnaliseAsync_DadosValidos_DevePersistirComSucessoEGerarId()
        {
            // Arrange
            using var context = CriarDbContextEmMemoria();
            var service = new PersistenciaService(context);

            var analise = new AnaliseSalva
            {
                UserId = "user-123",
                Contexto = "Análise de vendas do primeiro trimestre",
                ResultadosJson = "{\"total\": 15000}",
                DataCriacao = DateTime.UtcNow
            };

            // Act
            await service.SalvarAnaliseAsync(analise);

            // Assert
            analise.Id.Should().BeGreaterThan(0);

            var analiseNoBanco = await context.AnalisesSalvas.FirstOrDefaultAsync(a => a.Id == analise.Id);
            analiseNoBanco.Should().NotBeNull();
            analiseNoBanco!.UserId.Should().Be("user-123");
            analiseNoBanco.Contexto.Should().Be("Análise de vendas do primeiro trimestre");
            analiseNoBanco.ResultadosJson.Should().Be("{\"total\": 15000}");
        }

        [Fact]
        public async Task ListarHistoricoAsync_MultiTenant_DeveRetornarApenasAnalisesDoUsuarioEspecificado()
        {
            // Arrange
            using var context = CriarDbContextEmMemoria();
            var service = new PersistenciaService(context);

            context.AnalisesSalvas.AddRange(
                new AnaliseSalva { UserId = "usuario-a", Contexto = "Análise A1", ResultadosJson = "{}", DataCriacao = DateTime.UtcNow.AddMinutes(-10) },
                new AnaliseSalva { UserId = "usuario-a", Contexto = "Análise A2", ResultadosJson = "{}", DataCriacao = DateTime.UtcNow.AddMinutes(-5) },
                new AnaliseSalva { UserId = "usuario-b", Contexto = "Análise B1", ResultadosJson = "{}", DataCriacao = DateTime.UtcNow.AddMinutes(-2) }
            );
            await context.SaveChangesAsync();

            // Act
            var resultadoUsuarioA = await service.ListarHistoricoAsync("usuario-a");
            var resultadoUsuarioB = await service.ListarHistoricoAsync("usuario-b");

            // Assert
            resultadoUsuarioA.Should().HaveCount(2);
            resultadoUsuarioA.Should().OnlyContain(a => a.UserId == "usuario-a");

            resultadoUsuarioB.Should().HaveCount(1);
            resultadoUsuarioB.Should().OnlyContain(a => a.UserId == "usuario-b");
        }

        [Fact]
        public async Task ListarHistoricoAsync_MultiplasAnalises_DeveRetornarOrdenadoPorDataCriacaoDecrescente()
        {
            // Arrange
            using var context = CriarDbContextEmMemoria();
            var service = new PersistenciaService(context);
            var userId = "usuario-cronologico";

            var dataAntiga = new DateTime(2025, 1, 1, 10, 0, 0, DateTimeKind.Utc);
            var dataIntermediaria = new DateTime(2025, 1, 2, 10, 0, 0, DateTimeKind.Utc);
            var dataRecente = new DateTime(2025, 1, 3, 10, 0, 0, DateTimeKind.Utc);

            context.AnalisesSalvas.AddRange(
                new AnaliseSalva { UserId = userId, Contexto = "Antiga", ResultadosJson = "{}", DataCriacao = dataAntiga },
                new AnaliseSalva { UserId = userId, Contexto = "Recente", ResultadosJson = "{}", DataCriacao = dataRecente },
                new AnaliseSalva { UserId = userId, Contexto = "Intermediaria", ResultadosJson = "{}", DataCriacao = dataIntermediaria }
            );
            await context.SaveChangesAsync();

            // Act
            var resultado = await service.ListarHistoricoAsync(userId);

            // Assert
            resultado.Should().HaveCount(3);
            resultado[0].Contexto.Should().Be("Recente");
            resultado[1].Contexto.Should().Be("Intermediaria");
            resultado[2].Contexto.Should().Be("Antiga");
        }

        [Fact]
        public async Task ListarHistoricoAsync_UsuarioSemAnalises_DeveRetornarListaVazia()
        {
            // Arrange
            using var context = CriarDbContextEmMemoria();
            var service = new PersistenciaService(context);

            context.AnalisesSalvas.Add(
                new AnaliseSalva { UserId = "outro-usuario", Contexto = "Existente", ResultadosJson = "{}", DataCriacao = DateTime.UtcNow }
            );
            await context.SaveChangesAsync();

            // Act
            var resultado = await service.ListarHistoricoAsync("usuario-sem-dados");

            // Assert
            resultado.Should().NotBeNull();
            resultado.Should().BeEmpty();
        }

        [Fact]
        public async Task ObterAnalisePorIdAsync_IdEUsuarioCorrespondentes_DeveRetornarAnaliseCorreta()
        {
            // Arrange
            using var context = CriarDbContextEmMemoria();
            var service = new PersistenciaService(context);
            var userId = "usuario-proprietario";

            var analise = new AnaliseSalva
            {
                UserId = userId,
                Contexto = "Relatório Financeiro",
                ResultadosJson = "{\"lucro\": 50000}",
                DataCriacao = DateTime.UtcNow
            };
            context.AnalisesSalvas.Add(analise);
            await context.SaveChangesAsync();

            // Act
            var resultado = await service.ObterAnalisePorIdAsync(analise.Id, userId);

            // Assert
            resultado.Should().NotBeNull();
            resultado!.Id.Should().Be(analise.Id);
            resultado.UserId.Should().Be(userId);
            resultado.Contexto.Should().Be("Relatório Financeiro");
        }

        [Fact]
        public async Task ObterAnalisePorIdAsync_IdPertenceAOutroUsuario_DeveRetornarNull()
        {
            // Arrange
            using var context = CriarDbContextEmMemoria();
            var service = new PersistenciaService(context);

            var analise = new AnaliseSalva
            {
                UserId = "proprietario-real",
                Contexto = "Dados Sensíveis",
                ResultadosJson = "{\"segredo\": true}",
                DataCriacao = DateTime.UtcNow
            };
            context.AnalisesSalvas.Add(analise);
            await context.SaveChangesAsync();

            // Act
            var resultado = await service.ObterAnalisePorIdAsync(analise.Id, "usuario-intruso");

            // Assert
            resultado.Should().BeNull();
        }

        [Fact]
        public async Task ObterAnalisePorIdAsync_IdInexistente_DeveRetornarNull()
        {
            // Arrange
            using var context = CriarDbContextEmMemoria();
            var service = new PersistenciaService(context);

            // Act
            var resultado = await service.ObterAnalisePorIdAsync(99999, "qualquer-usuario");

            // Assert
            resultado.Should().BeNull();
        }
    }
}
