using QuiosqueBI.API.Models;

namespace QuiosqueBI.API.Services.Analise.Preparacao
{
    public interface IPreparacaoService
    {
        /// <summary>
        /// Sanitiza e normaliza os dados brutos lidos do arquivo (cabeçalhos, remoção de linhas vazias e fallbacks).
        /// </summary>
        DadosArquivo.RDadosArquivo PrepararDados(DadosArquivo.RDadosArquivo dadosDoArquivo);

        /// <summary>
        /// Converte com resiliência valores em formato de texto (pt-BR, en-US, monetário, contábil, percentual) para decimal.
        /// </summary>
        decimal ConverterParaDecimal(object? valor);
    }
}
