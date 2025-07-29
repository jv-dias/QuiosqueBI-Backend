using QuiosqueBI.API.Models;
using QuiosqueBI.API.Services.Analise.Fase1_Coleta;
using QuiosqueBI.API.Services.Analise.Fase3_Exploracao;
using QuiosqueBI.API.Services.Analise.Fase5_Persistencia;
using System.Security.Claims;
using System.Text.Json;

namespace QuiosqueBI.API.Services.Analise
{
    public class AnaliseOrquestradorService : IAnaliseOrquestradorService
    {
        private readonly IColetaService _coletaService;
        private readonly IExploracaoService _exploracaoService;
        private readonly IPersistenciaService _persistenciaService;

        public AnaliseOrquestradorService(IColetaService coletaService, IExploracaoService exploracaoService, IPersistenciaService persistenciaService)
        {
            _coletaService = coletaService;
            _exploracaoService = exploracaoService;
            _persistenciaService = persistenciaService;
        }

        public async Task<List<ResultadoGrafico>> ExecutarAnaliseCompletaAsync(IFormFile arquivo, string contexto, ClaimsPrincipal user)
        {
            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                throw new UnauthorizedAccessException("Usuário não autenticado.");
            }

            // Fases 1 e 3
            var dadosDoArquivo = await _coletaService.LerDadosDoArquivoAsync(arquivo);
            var resultados = await _exploracaoService.GerarInsightsIniciaisAsync(dadosDoArquivo, contexto);

            // Fase 5: O orquestrador é responsável por MONTAR o objeto
            if (resultados.Any())
            {
                var analiseParaSalvar = new AnaliseSalva
                {
                    Contexto = contexto,
                    DataCriacao = DateTime.UtcNow,
                    ResultadosJson = JsonSerializer.Serialize(resultados),
                    UserId = userId
                };
                // E então passa o objeto completo para o serviço de persistência
                await _persistenciaService.SalvarAnaliseAsync(analiseParaSalvar);
            }

            return resultados;
        }

        public async Task<List<AnaliseSalva>> ListarHistoricoAsync(ClaimsPrincipal user)
        {
            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                throw new UnauthorizedAccessException("Usuário não autenticado.");
            }
            return await _persistenciaService.ListarHistoricoAsync(userId);
        }

        public async Task<AnaliseSalva?> ObterAnalisePorIdAsync(int id, ClaimsPrincipal user)
        {
            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                throw new UnauthorizedAccessException("Usuário não autenticado.");
            }
            return await _persistenciaService.ObterAnalisePorIdAsync(id, userId);
        }
    }
}