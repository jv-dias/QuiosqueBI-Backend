using System.Dynamic;
using System.Globalization;
using System.Text.RegularExpressions;
using QuiosqueBI.API.Models;

namespace QuiosqueBI.API.Services.Analise.Preparacao
{
    public class PreparacaoService : IPreparacaoService
    {
        public DadosArquivo.RDadosArquivo PrepararDados(DadosArquivo.RDadosArquivo dadosDoArquivo)
        {
            if (dadosDoArquivo == null)
            {
                throw new ArgumentNullException(nameof(dadosDoArquivo));
            }

            // 1. Sanitizar cabeçalhos (Headers)
            var headersBrutos = dadosDoArquivo.Headers ?? Array.Empty<string>();
            var headersLimpos = new string[headersBrutos.Length];
            var cabecalhosVistos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < headersBrutos.Length; i++)
            {
                var h = headersBrutos[i]?.Replace('\u00A0', ' ').Trim() ?? string.Empty;
                h = Regex.Replace(h, @"[\r\n\t]+", " ").Trim();

                if (string.IsNullOrWhiteSpace(h))
                {
                    h = $"Coluna_{i + 1}";
                }

                // Garantir nomes únicos de cabeçalho
                var nomeBase = h;
                var sufixo = 2;
                while (cabecalhosVistos.Contains(h))
                {
                    h = $"{nomeBase}_{sufixo++}";
                }

                cabecalhosVistos.Add(h);
                headersLimpos[i] = h;
            }

            // 2. Sanitizar registros e descartar linhas 100% vazias
            var recordsBrutos = dadosDoArquivo.Records;
            if (recordsBrutos == null)
            {
                return new DadosArquivo.RDadosArquivo(headersLimpos, Enumerable.Empty<dynamic>());
            }

            var recordsLimpos = new List<dynamic>();

            foreach (var registro in recordsBrutos)
            {
                if (registro == null) continue;

                var dictOriginal = registro as IDictionary<string, object>;
                if (dictOriginal == null) continue;

                bool linhaPossuiDados = false;
                var novoRegistro = new ExpandoObject() as IDictionary<string, object>;

                for (int i = 0; i < headersBrutos.Length; i++)
                {
                    var headerOriginal = headersBrutos[i];
                    var headerLimpo = headersLimpos[i];

                    object? valorOriginal = null;
                    if (dictOriginal.ContainsKey(headerOriginal))
                    {
                        valorOriginal = dictOriginal[headerOriginal];
                    }
                    else if (dictOriginal.ContainsKey(headerLimpo))
                    {
                        valorOriginal = dictOriginal[headerLimpo];
                    }

                    if (valorOriginal != null)
                    {
                        if (valorOriginal is string str)
                        {
                            var strLimpa = Regex.Replace(str.Replace('\u00A0', ' '), @"[\r\n\t]+", " ").Trim();
                            if (!string.IsNullOrWhiteSpace(strLimpa))
                            {
                                linhaPossuiDados = true;
                                novoRegistro[headerLimpo] = strLimpa;
                            }
                            else
                            {
                                novoRegistro[headerLimpo] = "Não Identificado";
                            }
                        }
                        else
                        {
                            linhaPossuiDados = true;
                            novoRegistro[headerLimpo] = valorOriginal;
                        }
                    }
                    else
                    {
                        novoRegistro[headerLimpo] = "Não Identificado";
                    }
                }

                if (linhaPossuiDados)
                {
                    recordsLimpos.Add(novoRegistro);
                }
            }

            return new DadosArquivo.RDadosArquivo(headersLimpos, recordsLimpos);
        }

        public decimal ConverterParaDecimal(object? valor)
        {
            if (valor == null) return 0m;

            switch (valor)
            {
                case decimal m:
                    return m;
                case int i:
                    return i;
                case long l:
                    return l;
                case double d when !double.IsNaN(d) && !double.IsInfinity(d):
                    return Convert.ToDecimal(d);
                case float f when !float.IsNaN(f) && !float.IsInfinity(f):
                    return Convert.ToDecimal(f);
                case short s:
                    return s;
                case byte b:
                    return b;
            }

            var texto = valor.ToString()?.Replace('\u00A0', ' ').Trim();
            if (string.IsNullOrWhiteSpace(texto)) return 0m;

            // Se for o fallback textual padrão, retorna zero
            if (string.Equals(texto, "Não Identificado", StringComparison.OrdinalIgnoreCase))
            {
                return 0m;
            }

            // Detectar notação contábil com parênteses: (1.500,00) ou (1500)
            var isNegativo = false;
            if (texto.StartsWith("(") && texto.EndsWith(")"))
            {
                isNegativo = true;
                texto = texto.Substring(1, texto.Length - 2).Trim();
            }

            if (texto.StartsWith("-"))
            {
                isNegativo = !isNegativo;
                texto = texto.Substring(1).Trim();
            }
            else if (texto.StartsWith("+"))
            {
                texto = texto.Substring(1).Trim();
            }

            // Remover símbolos monetários, percentuais e códigos de moeda
            texto = texto
                .Replace("R$", "", StringComparison.OrdinalIgnoreCase)
                .Replace("$", "")
                .Replace("€", "")
                .Replace("£", "")
                .Replace("%", "")
                .Replace("BRL", "", StringComparison.OrdinalIgnoreCase)
                .Replace("USD", "", StringComparison.OrdinalIgnoreCase)
                .Trim();

            if (string.IsNullOrWhiteSpace(texto)) return 0m;

            // Analisar separadores de milhar e decimal
            var temVirgula = texto.Contains(',');
            var temPonto = texto.Contains('.');

            if (temVirgula && temPonto)
            {
                var ultimoPonto = texto.LastIndexOf('.');
                var ultimaVirgula = texto.LastIndexOf(',');

                if (ultimaVirgula > ultimoPonto)
                {
                    // Formato pt-BR: 1.500.000,50 -> remove ponto de milhar e troca vírgula decimal por ponto
                    texto = texto.Replace(".", "").Replace(",", ".");
                }
                else
                {
                    // Formato en-US: 1,500,000.50 -> remove vírgula de milhar
                    texto = texto.Replace(",", "");
                }
            }
            else if (temVirgula)
            {
                var totalVirgulas = texto.Count(c => c == ',');
                if (totalVirgulas > 1)
                {
                    // Múltiplas vírgulas: milhar sem decimal (ex: 1,000,000)
                    texto = texto.Replace(",", "");
                }
                else
                {
                    // Única vírgula: separador decimal pt-BR (ex: 1500,50 ou 12,34)
                    texto = texto.Replace(",", ".");
                }
            }
            else if (temPonto)
            {
                var totalPontos = texto.Count(c => c == '.');
                if (totalPontos > 1)
                {
                    // Múltiplos pontos: milhar pt-BR (ex: 1.000.000)
                    texto = texto.Replace(".", "");
                }
            }

            if (decimal.TryParse(texto, NumberStyles.Any, CultureInfo.InvariantCulture, out var resultadoDecimal))
            {
                return isNegativo ? -resultadoDecimal : resultadoDecimal;
            }

            return 0m;
        }
    }
}
