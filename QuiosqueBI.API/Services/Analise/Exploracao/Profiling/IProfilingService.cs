using QuiosqueBI.API.Models;

namespace QuiosqueBI.API.Services.Analise.Exploracao.Profiling
{
    public interface IProfilingService
    {
        /// <summary>
        /// Realiza a descoberta de metadados e classificação semântica das colunas da planilha.
        /// </summary>
        /// <param name="dados">Conjunto sanitizado de dados tabulares.</param>
        /// <returns>Lista com o perfil analítico e semântico de cada coluna.</returns>
        List<PerfilColuna> AnalisarColunas(DadosArquivo.RDadosArquivo dados);
    }
}
