using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;

namespace QuiosqueBI.API.Services;

public interface IAuthService
{
    Task<IActionResult> RegisterAsync(string nome, string sobrenome, string email, string password);
    Task<IActionResult> LoginAsync(string email, string password);
}
