using Microsoft.AspNetCore.Mvc;
using QuiosqueBI.API.Services;

// DTOs (Data Transfer Objects) para receber os dados do frontend
public record RegisterDto(string Nome, string Sobrenome, string Email, string Password);
public record LoginDto(string Email, string Password);

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterDto model)
    {
        return await _authService.RegisterAsync(model.Nome, model.Sobrenome, model.Email, model.Password);
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginDto model)
    {
        return await _authService.LoginAsync(model.Email, model.Password);
    }
}