using AwesomeAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;
using QuiosqueBI.API.Services;

namespace QuiosqueBI.Tests.Controllers
{
    public class AuthControllerTests
    {
        [Fact]
        public async Task Register_ChamadaValida_DeveRepassarParametrosParaAuthService()
        {
            // Arrange
            var authServiceMock = new Mock<IAuthService>();
            var model = new RegisterDto("Carlos", "Eduardo", "carlos@email.com", "SenhaForte@123");
            var respostaEsperada = new OkObjectResult(new { Message = "Usuário criado com sucesso!" });

            authServiceMock.Setup(s => s.RegisterAsync(model.Nome, model.Sobrenome, model.Email, model.Password))
                .ReturnsAsync(respostaEsperada);

            var controller = new AuthController(authServiceMock.Object);

            // Act
            var resultado = await controller.Register(model);

            // Assert
            resultado.Should().BeSameAs(respostaEsperada);
            authServiceMock.Verify(s => s.RegisterAsync("Carlos", "Eduardo", "carlos@email.com", "SenhaForte@123"), Times.Once);
        }

        [Fact]
        public async Task Login_ChamadaValida_DeveRepassarParametrosParaAuthService()
        {
            // Arrange
            var authServiceMock = new Mock<IAuthService>();
            var model = new LoginDto("carlos@email.com", "SenhaForte@123");
            var respostaEsperada = new OkObjectResult(new { token = "token-jwt-simulado" });

            authServiceMock.Setup(s => s.LoginAsync(model.Email, model.Password))
                .ReturnsAsync(respostaEsperada);

            var controller = new AuthController(authServiceMock.Object);

            // Act
            var resultado = await controller.Login(model);

            // Assert
            resultado.Should().BeSameAs(respostaEsperada);
            authServiceMock.Verify(s => s.LoginAsync("carlos@email.com", "SenhaForte@123"), Times.Once);
        }
    }
}
