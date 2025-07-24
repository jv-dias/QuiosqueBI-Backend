using Microsoft.AspNetCore.Mvc;
using MediatR;
using QuiosqueBI.API.Features.Auth;


// DTOs (Data Transfer Objects) para receber os dados do frontend
public record RegisterDto(string Nome, string Sobrenome, string Email, string Password);
public record LoginDto(string Email, string Password);

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IMediator _mediator;

    public AuthController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterDto model)
    {
        var command = new Register.Command(model.Nome, model.Sobrenome, model.Email, model.Password);
        return await _mediator.Send(command);
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginDto model)
    {
        var command = new Login.Command(model.Email, model.Password);
        return await _mediator.Send(command);
    }
}