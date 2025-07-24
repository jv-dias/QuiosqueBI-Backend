using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MediatR;
using QuiosqueBI.API.Features.Analises;

namespace QuiosqueBI.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class AnaliseController : ControllerBase
{
    private readonly IMediator _mediator;

    public AnaliseController(IMediator mediator)
    {
        _mediator = mediator;
    }

    // Rota para upload de arquivo e geração de resultados
    [HttpPost("upload")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Upload(IFormFile arquivo, [FromForm] string contexto)
    {
        var command = new UploadAnalise.Command(arquivo, contexto, User);
        return await _mediator.Send(command);
    }

    // Rota para depuração, que retorna dados brutos e sugestões da IA
    [HttpPost("debug")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetDebugData(IFormFile arquivo, [FromForm] string contexto)
    {
        var query = new DebugAnalise.Query(arquivo, contexto);
        return await _mediator.Send(query);
    }

    // Rota para listar análises salvas
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    [HttpGet("historico")]
    public async Task<IActionResult> ListarHistorico()
    {
        var query = new ListarHistorico.Query(User);
        return await _mediator.Send(query);
    }

    // Rota para obter uma análise salva por ID
    [HttpGet("historico/{id}")]
    public async Task<IActionResult> ObterHistoricoPorId(int id)
    {
        var query = new ObterAnalisePorId.Query(id, User);
        return await _mediator.Send(query);
    }
}
