using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace QuiosqueBI.API.Services;

public class AuthService : IAuthService
{
    private readonly UserManager<IdentityUser> _userManager;
    private readonly IConfiguration _configuration;

    public AuthService(UserManager<IdentityUser> userManager, IConfiguration configuration)
    {
        _userManager = userManager;
        _configuration = configuration;
    }

    public async Task<IActionResult> RegisterAsync(string nome, string sobrenome, string email, string password)
    {
        var userExists = await _userManager.FindByEmailAsync(email);
        if (userExists != null)
            return new ConflictObjectResult(new { Message = "Usuário já existe!" });

        // Gera o UserName base
        var baseUserName = $"{nome}-{sobrenome}";
        var userName = baseUserName;
        int suffix = 1;

        // Garante unicidade do UserName adicionando um número se necessário
        while (await _userManager.FindByNameAsync(userName) != null)
        {
            userName = $"{baseUserName}{suffix}";
            suffix++;
        }

        var user = new IdentityUser()
        {
            Email = email,
            SecurityStamp = Guid.NewGuid().ToString(),
            UserName = userName
        };

        var result = await _userManager.CreateAsync(user, password);
        if (!result.Succeeded)
            return new ObjectResult(new { Message = "Falha ao criar usuário!", Errors = result.Errors })
            {
                StatusCode = 500
            };

        await _userManager.AddToRoleAsync(user, "Usuario");

        return new OkObjectResult(new { Message = "Usuário criado com sucesso!" });
    }

    public async Task<IActionResult> LoginAsync(string email, string password)
    {
        var user = await _userManager.FindByEmailAsync(email);
        if (user != null && await _userManager.CheckPasswordAsync(user, password))
        {
            var token = GerarTokenJwt(user);
            return new OkObjectResult(new { token });
        }
        return new UnauthorizedObjectResult(new { Message = "Credenciais inválidas." });
    }

    private string GerarTokenJwt(IdentityUser user)
    {
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id),
            new Claim(ClaimTypes.Email, user.Email ?? string.Empty),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.Name, user.UserName ?? string.Empty)
        };

        var authSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:SecretKey"] ?? throw new InvalidOperationException("JWT SecretKey not configured")));

        var token = new JwtSecurityToken(
            issuer: _configuration["Jwt:Issuer"],
            audience: _configuration["Jwt:Audience"],
            expires: DateTime.Now.AddHours(3),
            claims: claims,
            signingCredentials: new SigningCredentials(authSigningKey, SecurityAlgorithms.HmacSha256)
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
