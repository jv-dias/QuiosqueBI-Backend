using QuiosqueBI.API.Models;

namespace QuiosqueBI.API.Services.Analise.Fase3_Exploracao
{
	public interface IExploracaoService
	{
		// Recebe os dados lidos e o contexto, e retorna os gráficos processados
		Task<List<ResultadoGrafico>> GerarInsightsIniciaisAsync(DadosArquivo.RDadosArquivo dadosDoArquivo, string contexto);
	}
}