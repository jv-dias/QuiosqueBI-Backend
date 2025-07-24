using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuiosqueBI.API.Data;
using QuiosqueBI.API.Models;
using System.Security.Claims;

namespace QuiosqueBI.API.Features.Analises;

public static class ListarHistorico
{
    public record Query(ClaimsPrincipal User) : IRequest<IActionResult>;

    public class Handler : IRequestHandler<Query, IActionResult>
    {
        private readonly ApplicationDbContext _context;

        public Handler(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Handle(Query request, CancellationToken cancellationToken)
        {
            var userId = request.User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                return new UnauthorizedObjectResult("Não foi possível identificar o usuário.");
            }

            try
            {
                var historico = await _context.AnalisesSalvas
                    .Where(a => a.UserId == userId)
                    .OrderByDescending(a => a.DataCriacao)
                    .ToListAsync(cancellationToken);

                return new OkObjectResult(historico);
            }
            catch (Exception ex)
            {
                return new ObjectResult($"Ocorreu um erro interno: {ex.Message}")
                {
                    StatusCode = 500
                };
            }
        }
    }
}
