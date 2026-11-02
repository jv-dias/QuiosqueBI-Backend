namespace QuiosqueBI.API.Services.Analise.Exploracao.Profiling
{
    /// <summary>
    /// Metadados descritivos e classificação semântica de uma coluna tabular.
    /// </summary>
    public record PerfilColuna(
        int Indice,
        string Nome,
        TipoSemanticoColuna TipoSemantico,
        int CardinalidadeAmostra,
        double TaxaUnicidade,
        List<string> ExemplosAmostra,
        bool EhMetricaSugerida,
        bool EhDimensaoSugerida
    );
}
