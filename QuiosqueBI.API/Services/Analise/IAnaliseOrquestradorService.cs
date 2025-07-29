using QuiosqueBI.API.Models;
using System.Security.Claims;

namespace QuiosqueBI.API.Services.Analise
{
	public interface IAnaliseOrquestradorService
	{
		Task<List<ResultadoGrafico>> ExecutarAnaliseCompletaAsync(IFormFile arquivo, string contexto, ClaimsPrincipal user);
		Task<List<AnaliseSalva>> ListarHistoricoAsync(ClaimsPrincipal user);
		Task<AnaliseSalva?> ObterAnalisePorIdAsync(int id, ClaimsPrincipal user);
	}
}