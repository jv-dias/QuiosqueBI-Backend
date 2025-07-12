namespace QuiosqueBI.API.Models;

public class DadosArquivo
{
    public record RDadosArquivo(string[] Headers, IEnumerable<dynamic> Records);
}