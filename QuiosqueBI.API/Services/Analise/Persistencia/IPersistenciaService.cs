using QuiosqueBI.API.Models;

namespace QuiosqueBI.API.Services.Analise.Fase5_Persistencia
{
	public interface IPersistenciaService
	{
		// O método agora espera receber o objeto 'AnaliseSalva' completo
		Task SalvarAnaliseAsync(AnaliseSalva analise);
		Task<List<AnaliseSalva>> ListarHistoricoAsync(string userId);
		Task<AnaliseSalva?> ObterAnalisePorIdAsync(int id, string userId);
	}
}