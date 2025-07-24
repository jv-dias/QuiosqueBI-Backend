using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuiosqueBI.API.Data;
using System.Security.Claims;

namespace QuiosqueBI.API.Features.Analises;

public static class ObterAnalisePorId
{
    public record Query(int Id, ClaimsPrincipal User) : IRequest<IActionResult>;

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
                var analise = await _context.AnalisesSalvas
                    .FirstOrDefaultAsync(a => a.Id == request.Id && a.UserId == userId, cancellationToken);

                if (analise == null)
                {
                    return new NotFoundResult();
                }

                return new OkObjectResult(analise);
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
