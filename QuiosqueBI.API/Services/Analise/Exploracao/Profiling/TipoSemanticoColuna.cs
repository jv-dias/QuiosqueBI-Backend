namespace QuiosqueBI.API.Services.Analise.Exploracao.Profiling
{
    /// <summary>
    /// Classificação semântica para colunas de dados tabulares.
    /// </summary>
    public enum TipoSemanticoColuna
    {
        /// <summary>Valores temporais, datas, timestamps e períodos.</summary>
        DataTemporal,

        /// <summary>Códigos, chaves, identificadores e CPFs com alta taxa de unicidade (nunca devem ser agregados por soma).</summary>
        TextoIdentificador,

        /// <summary>Categorias nominais e atributos qualitativos repetíveis, ideais para dimensões de agrupamento.</summary>
        TextoCategoria,

        /// <summary>Valores monetários, financeiros e monetários com precisão decimal (métricas contínuas).</summary>
        NumericoMonetario,

        /// <summary>Valores discretos de contagem, volumes, unidades e escores numéricos (métricas quantitativas).</summary>
        NumericoQuantidade,

        /// <summary>Descrições longas, observações livres e narrativas não categorizáveis.</summary>
        TextoLivre
    }
}
