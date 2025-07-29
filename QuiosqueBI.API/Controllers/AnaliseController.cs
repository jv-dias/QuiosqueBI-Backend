using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuiosqueBI.API.Services.Analise;

namespace QuiosqueBI.API.Controllers
{
    [Authorize] // Protege todos os endpoints neste controller
    [ApiController]
    [Route("api/[controller]")]
    public class AnaliseController : ControllerBase
    {
        private readonly IAnaliseOrquestradorService _orquestradorService;

        public AnaliseController(IAnaliseOrquestradorService orquestradorService)
        {
            _orquestradorService = orquestradorService;
        }

        [HttpPost("upload")]
        public async Task<IActionResult> Upload(IFormFile arquivo, [FromForm] string contexto)
        {
            try
            {
                var resultados = await _orquestradorService.ExecutarAnaliseCompletaAsync(arquivo, contexto, User);
                return Ok(new { Resultados = resultados });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Ocorreu um erro interno: {ex.Message}");
            }
        }

        [HttpGet("historico")]
        public async Task<IActionResult> ListarHistorico()
        {
            try
            {
                var historico = await _orquestradorService.ListarHistoricoAsync(User);
                return Ok(historico);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Ocorreu um erro interno: {ex.Message}");
            }
        }

        [HttpGet("historico/{id}")]
        public async Task<IActionResult> ObterAnalisePorId(int id)
        {
            try
            {
                var analise = await _orquestradorService.ObterAnalisePorIdAsync(id, User);
                if (analise == null)
                {
                    return NotFound();
                }
                return Ok(analise);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Ocorreu um erro interno: {ex.Message}");
            }
        }
    }
}