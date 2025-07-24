using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace QuiosqueBI.API.Features.Auth;

public static class Register
{
    public record Command(string Nome, string Sobrenome, string Email, string Password) : IRequest<IActionResult>;

    public class Handler : IRequestHandler<Command, IActionResult>
    {
        private readonly UserManager<IdentityUser> _userManager;

        public Handler(UserManager<IdentityUser> userManager)
        {
            _userManager = userManager;
        }

        public async Task<IActionResult> Handle(Command request, CancellationToken cancellationToken)
        {
            var userExists = await _userManager.FindByEmailAsync(request.Email);
            if (userExists != null)
                return new ConflictObjectResult(new { Message = "Usuário já existe!" });

            var user = new IdentityUser()
            {
                Email = request.Email,
                SecurityStamp = Guid.NewGuid().ToString(),
                UserName = $"{request.Nome}-{request.Sobrenome}"
            };

            var result = await _userManager.CreateAsync(user, request.Password);
            if (!result.Succeeded)
                return new ObjectResult(new { Message = "Falha ao criar usuário!", Errors = result.Errors })
                {
                    StatusCode = 500
                };

            await _userManager.AddToRoleAsync(user, "Usuario");

            return new OkObjectResult(new { Message = "Usuário criado com sucesso!" });
        }
    }
}
