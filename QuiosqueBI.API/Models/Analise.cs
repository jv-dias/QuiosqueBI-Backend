namespace QuiosqueBI.API.Models;

// Representa a sugestão de análise que vem da IA, agora com índices
// Os índices são usados para identificar a dimensão e métrica sugeridas
// que devem ser usadas para gerar o gráfico.
public class AnaliseSugerida
{
    public string? titulo_grafico { get; set; }
    public string? tipo_grafico { get; set; }
    public int indice_dimensao { get; set; }
    public int indice_metrica { get; set; }
}

// Representa o resultado final que enviaremos para o front-end
public class ResultadoGrafico
{
    public string? Titulo { get; set; }
    public string? TipoGrafico { get; set; }
    public IEnumerable<object>? Dados { get; set; }
}

// Representa os dados de depuração que retornaremos na rota /debug
// Contém os cabeçalhos do arquivo, os dados brutos (as 10 primeiras linhas) e o plano da IA
// que sugere análises com base nesses dados.
public class DebugData
{
    public IEnumerable<string>? CabecalhosDoArquivo { get; set; }
    public IEnumerable<object>? DadosBrutosDoArquivo { get; set; } 
    public IEnumerable<AnaliseSugerida>? PlanoDaIA { get; set; }
}