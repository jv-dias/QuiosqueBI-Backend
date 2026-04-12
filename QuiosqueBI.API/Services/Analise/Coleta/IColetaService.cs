using QuiosqueBI.API.Models;

namespace QuiosqueBI.API.Services.Analise.Fase1_Coleta
{
	public interface IColetaService
	{
		// Este método recebe o ficheiro e retorna os dados lidos (cabeçalhos e registos)
		Task<DadosArquivo.RDadosArquivo> LerDadosDoArquivoAsync(IFormFile arquivo);
	}
}