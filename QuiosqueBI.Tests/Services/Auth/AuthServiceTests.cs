using AwesomeAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Moq;
using QuiosqueBI.API.Services;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace QuiosqueBI.Tests.Services.Auth
{
    public class AuthServiceTests
    {
        private static Mock<UserManager<IdentityUser>> CriarMockUserManager()
        {
            var store = new Mock<IUserStore<IdentityUser>>();
            return new Mock<UserManager<IdentityUser>>(
                store.Object, null!, null!, null!, null!, null!, null!, null!, null!
            );
        }

        private static IConfiguration CriarConfiguracaoJwt()
        {
            var configuracoes = new Dictionary<string, string?>
            {
                { "Jwt:SecretKey", "ChaveSuperSecretaDePeloMenos32BytesParaTokenJwtQuiosqueBI2025!" },
                { "Jwt:Issuer", "QuiosqueBI-API" },
                { "Jwt:Audience", "QuiosqueBI-Audience" }
            };

            return new ConfigurationBuilder()
                .AddInMemoryCollection(configuracoes)
                .Build();
        }

        [Fact]
        public async Task RegisterAsync_EmailJaCadastrado_DeveRetornarConflict409()
        {
            // Arrange
            var userManagerMock = CriarMockUserManager();
            var configuration = CriarConfiguracaoJwt();
            var email = "usuario.existente@email.com";

            userManagerMock.Setup(m => m.FindByEmailAsync(email))
                .ReturnsAsync(new IdentityUser { Email = email });

            var service = new AuthService(userManagerMock.Object, configuration);

            // Act
            var resultado = await service.RegisterAsync("Carlos", "Eduardo", email, "SenhaForte@123");

            // Assert
            var conflict = resultado.Should().BeOfType<ConflictObjectResult>().Subject;
            conflict.StatusCode.Should().Be(409);
        }

        [Fact]
        public async Task RegisterAsync_DadosValidos_DeveCriarUsuarioComRoleUsuarioERetornarOk200()
        {
            // Arrange
            var userManagerMock = CriarMockUserManager();
            var configuration = CriarConfiguracaoJwt();
            var email = "novo.usuario@email.com";

            userManagerMock.Setup(m => m.FindByEmailAsync(email))
                .ReturnsAsync((IdentityUser?)null);
            userManagerMock.Setup(m => m.FindByNameAsync("Carlos-Eduardo"))
                .ReturnsAsync((IdentityUser?)null);
            userManagerMock.Setup(m => m.CreateAsync(It.IsAny<IdentityUser>(), "SenhaForte@123"))
                .ReturnsAsync(IdentityResult.Success);
            userManagerMock.Setup(m => m.AddToRoleAsync(It.IsAny<IdentityUser>(), "Usuario"))
                .ReturnsAsync(IdentityResult.Success);

            var service = new AuthService(userManagerMock.Object, configuration);

            // Act
            var resultado = await service.RegisterAsync("Carlos", "Eduardo", email, "SenhaForte@123");

            // Assert
            var okResult = resultado.Should().BeOfType<OkObjectResult>().Subject;
            okResult.StatusCode.Should().Be(200);

            userManagerMock.Verify(m => m.CreateAsync(
                It.Is<IdentityUser>(u => u.Email == email && u.UserName == "Carlos-Eduardo"),
                "SenhaForte@123"), Times.Once);

            userManagerMock.Verify(m => m.AddToRoleAsync(
                It.Is<IdentityUser>(u => u.UserName == "Carlos-Eduardo"),
                "Usuario"), Times.Once);
        }

        [Fact]
        public async Task RegisterAsync_UserNameJaExistente_DeveAdicionarSufixoNumericoParaGarantirUnicidade()
        {
            // Arrange
            var userManagerMock = CriarMockUserManager();
            var configuration = CriarConfiguracaoJwt();
            var email = "carlos2@email.com";

            userManagerMock.Setup(m => m.FindByEmailAsync(email))
                .ReturnsAsync((IdentityUser?)null);
            
            // Simula que "Carlos-Eduardo" já existe, mas "Carlos-Eduardo1" está vago
            userManagerMock.Setup(m => m.FindByNameAsync("Carlos-Eduardo"))
                .ReturnsAsync(new IdentityUser { UserName = "Carlos-Eduardo" });
            userManagerMock.Setup(m => m.FindByNameAsync("Carlos-Eduardo1"))
                .ReturnsAsync((IdentityUser?)null);

            userManagerMock.Setup(m => m.CreateAsync(It.IsAny<IdentityUser>(), "SenhaForte@123"))
                .ReturnsAsync(IdentityResult.Success);
            userManagerMock.Setup(m => m.AddToRoleAsync(It.IsAny<IdentityUser>(), "Usuario"))
                .ReturnsAsync(IdentityResult.Success);

            var service = new AuthService(userManagerMock.Object, configuration);

            // Act
            var resultado = await service.RegisterAsync("Carlos", "Eduardo", email, "SenhaForte@123");

            // Assert
            var okResult = resultado.Should().BeOfType<OkObjectResult>().Subject;
            okResult.StatusCode.Should().Be(200);

            userManagerMock.Verify(m => m.CreateAsync(
                It.Is<IdentityUser>(u => u.UserName == "Carlos-Eduardo1"),
                "SenhaForte@123"), Times.Once);
        }

        [Fact]
        public async Task RegisterAsync_FalhaAoCriarUsuario_DeveRetornarStatus500ComErros()
        {
            // Arrange
            var userManagerMock = CriarMockUserManager();
            var configuration = CriarConfiguracaoJwt();
            var email = "falha@email.com";

            userManagerMock.Setup(m => m.FindByEmailAsync(email))
                .ReturnsAsync((IdentityUser?)null);
            userManagerMock.Setup(m => m.FindByNameAsync(It.IsAny<string>()))
                .ReturnsAsync((IdentityUser?)null);

            var erroIdentity = new IdentityError { Code = "PasswordTooShort", Description = "A senha é muito curta." };
            userManagerMock.Setup(m => m.CreateAsync(It.IsAny<IdentityUser>(), "123"))
                .ReturnsAsync(IdentityResult.Failed(erroIdentity));

            var service = new AuthService(userManagerMock.Object, configuration);

            // Act
            var resultado = await service.RegisterAsync("Carlos", "Eduardo", email, "123");

            // Assert
            var objResult = resultado.Should().BeOfType<ObjectResult>().Subject;
            objResult.StatusCode.Should().Be(500);
        }

        [Fact]
        public async Task LoginAsync_CredenciaisValidas_DeveRetornarOk200ComTokenJwtValido()
        {
            // Arrange
            var userManagerMock = CriarMockUserManager();
            var configuration = CriarConfiguracaoJwt();
            var email = "usuario.valido@email.com";
            var senha = "SenhaCorreta@123";

            var usuario = new IdentityUser
            {
                Id = "guid-usuario-456",
                Email = email,
                UserName = "Usuario-Valido"
            };

            userManagerMock.Setup(m => m.FindByEmailAsync(email))
                .ReturnsAsync(usuario);
            userManagerMock.Setup(m => m.CheckPasswordAsync(usuario, senha))
                .ReturnsAsync(true);

            var service = new AuthService(userManagerMock.Object, configuration);

            // Act
            var resultado = await service.LoginAsync(email, senha);

            // Assert
            var okResult = resultado.Should().BeOfType<OkObjectResult>().Subject;
            okResult.StatusCode.Should().Be(200);

            // Extrai o token do envelope anônimo
            var valorDinamico = okResult.Value;
            valorDinamico.Should().NotBeNull();
            var propriedadeToken = valorDinamico!.GetType().GetProperty("token");
            propriedadeToken.Should().NotBeNull();
            var tokenString = propriedadeToken!.GetValue(valorDinamico) as string;
            tokenString.Should().NotBeNullOrWhiteSpace();

            // Decodifica e inspeciona o token JWT emitido
            var handler = new JwtSecurityTokenHandler();
            var jwtSecurityToken = handler.ReadJwtToken(tokenString);

            jwtSecurityToken.Issuer.Should().Be("QuiosqueBI-API");
            jwtSecurityToken.Audiences.Should().Contain("QuiosqueBI-Audience");
            jwtSecurityToken.Claims.First(c => c.Type == ClaimTypes.NameIdentifier).Value.Should().Be("guid-usuario-456");
            jwtSecurityToken.Claims.First(c => c.Type == ClaimTypes.Email).Value.Should().Be(email);
            jwtSecurityToken.Claims.First(c => c.Type == ClaimTypes.Name).Value.Should().Be("Usuario-Valido");
            jwtSecurityToken.Claims.Any(c => c.Type == JwtRegisteredClaimNames.Jti).Should().BeTrue();
        }

        [Fact]
        public async Task LoginAsync_UsuarioNaoEncontrado_DeveRetornarUnauthorized401()
        {
            // Arrange
            var userManagerMock = CriarMockUserManager();
            var configuration = CriarConfiguracaoJwt();
            var email = "naoexiste@email.com";

            userManagerMock.Setup(m => m.FindByEmailAsync(email))
                .ReturnsAsync((IdentityUser?)null);

            var service = new AuthService(userManagerMock.Object, configuration);

            // Act
            var resultado = await service.LoginAsync(email, "qualquerSenha");

            // Assert
            var unauth = resultado.Should().BeOfType<UnauthorizedObjectResult>().Subject;
            unauth.StatusCode.Should().Be(401);
        }

        [Fact]
        public async Task LoginAsync_SenhaIncorreta_DeveRetornarUnauthorized401()
        {
            // Arrange
            var userManagerMock = CriarMockUserManager();
            var configuration = CriarConfiguracaoJwt();
            var email = "usuario@email.com";

            var usuario = new IdentityUser { Id = "guid-1", Email = email, UserName = "User" };

            userManagerMock.Setup(m => m.FindByEmailAsync(email))
                .ReturnsAsync(usuario);
            userManagerMock.Setup(m => m.CheckPasswordAsync(usuario, "SenhaInvalida"))
                .ReturnsAsync(false);

            var service = new AuthService(userManagerMock.Object, configuration);

            // Act
            var resultado = await service.LoginAsync(email, "SenhaInvalida");

            // Assert
            var unauth = resultado.Should().BeOfType<UnauthorizedObjectResult>().Subject;
            unauth.StatusCode.Should().Be(401);
        }
    }
}
