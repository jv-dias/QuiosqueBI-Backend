using QuiosqueBI.API.Data;
using QuiosqueBI.API.Models;
using Microsoft.EntityFrameworkCore;

namespace QuiosqueBI.API.Services.Analise.Fase5_Persistencia
{
	public class PersistenciaService : IPersistenciaService
	{
		private readonly ApplicationDbContext _context;

		public PersistenciaService(ApplicationDbContext context)
		{
			_context = context;
		}
       
		// Agora o método recebe o objeto 'analise' já pronto do orquestrador
		public async Task SalvarAnaliseAsync(AnaliseSalva analise)
		{
			// A sua única responsabilidade é adicionar e salvar.
			_context.AnalisesSalvas.Add(analise);
			await _context.SaveChangesAsync();
		}

		public async Task<List<AnaliseSalva>> ListarHistoricoAsync(string userId)
		{
			return await _context.AnalisesSalvas
				.Where(a => a.UserId == userId)
				.OrderByDescending(a => a.DataCriacao)
				.ToListAsync();
		}

		public async Task<AnaliseSalva?> ObterAnalisePorIdAsync(int id, string userId)
		{
			return await _context.AnalisesSalvas
				.FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId);
		}
	}
}