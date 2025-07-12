using QuiosqueBI.API.Models;

namespace QuiosqueBI.API.Services
{
    public interface IAnaliseService
    {
        // Método que encapsula toda a lógica da rota /upload
        Task<List<ResultadoGrafico>> GerarResultadosAnaliseAsync(IFormFile arquivo, string contexto, string userId);
        
        // Método que encapsula toda a lógica da rota /debug
        Task<DebugData> GerarDadosDebugAsync(IFormFile arquivo, string contexto);
        
        // Método para listar análises salvas
        Task<List<AnaliseSalva>> ListarAnalisesSalvasAsync(string userId);
        
        // Método para Obter uma análise salva por ID
        Task<AnaliseSalva?> ObterAnaliseSalvaPorIdAsync(int id, string userId);
    }
}