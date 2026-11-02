using System.Dynamic;
using AwesomeAssertions;
using QuiosqueBI.API.Models;
using QuiosqueBI.API.Services.Analise.Exploracao.Profiling;
using QuiosqueBI.API.Services.Analise.Preparacao;
using Xunit;

namespace QuiosqueBI.Tests.Services.Analise.Exploracao.Profiling
{
    public class ProfilingServiceTests
    {
        private readonly ProfilingService _sut;

        public ProfilingServiceTests()
        {
            var preparacaoService = new PreparacaoService();
            _sut = new ProfilingService(preparacaoService);
        }

        [Fact]
        public void AnalisarColunas_ComTiposVariados_DeveClassificarCorretamente()
        {
            // Arrange
            var headers = new[] { "Cod_Pedido", "Data_Venda", "Categoria", "Valor_Total", "Quantidade", "Observacoes" };
            var rows = new List<dynamic>();

            var categorias = new[] { "Bebidas", "Alimentos", "Limpeza", "Bebidas", "Alimentos" };

            for (int i = 0; i < 20; i++)
            {
                dynamic r = new ExpandoObject();
                var d = (IDictionary<string, object>)r;
                d["Cod_Pedido"] = $"PED_{1000 + i}";
                d["Data_Venda"] = $"2024-0{1 + (i % 9)}-15";
                d["Categoria"] = categorias[i % categorias.Length];
                d["Valor_Total"] = $"R$ {(i + 1) * 25},50";
                d["Quantidade"] = (i + 1).ToString();
                d["Observacoes"] = "Esta é uma observação descritiva detalhada sobre o pedido efetuado pelo cliente no quiosque.";
                rows.Add(r);
            }

            var dados = new DadosArquivo.RDadosArquivo(headers, rows);

            // Act
            var perfis = _sut.AnalisarColunas(dados);

            // Assert
            perfis.Should().HaveCount(6);

            // 1. Identificador
            var pId = perfis.First(p => p.Nome == "Cod_Pedido");
            pId.TipoSemantico.Should().Be(TipoSemanticoColuna.TextoIdentificador);
            pId.EhMetricaSugerida.Should().BeFalse();
            pId.ExemplosAmostra.Should().Contain("[Anonimizado LGPD]");

            // 2. Data
            var pData = perfis.First(p => p.Nome == "Data_Venda");
            pData.TipoSemantico.Should().Be(TipoSemanticoColuna.DataTemporal);
            pData.EhDimensaoSugerida.Should().BeTrue();
            pData.EhMetricaSugerida.Should().BeFalse();

            // 3. Categoria
            var pCat = perfis.First(p => p.Nome == "Categoria");
            pCat.TipoSemantico.Should().Be(TipoSemanticoColuna.TextoCategoria);
            pCat.EhDimensaoSugerida.Should().BeTrue();
            pCat.CardinalidadeAmostra.Should().Be(3);
            pCat.ExemplosAmostra.Should().NotContain("[Anonimizado LGPD]");

            // 4. Moeda
            var pMoeda = perfis.First(p => p.Nome == "Valor_Total");
            pMoeda.TipoSemantico.Should().Be(TipoSemanticoColuna.NumericoMonetario);
            pMoeda.EhMetricaSugerida.Should().BeTrue();

            // 5. Quantidade
            var pQtd = perfis.First(p => p.Nome == "Quantidade");
            pQtd.TipoSemantico.Should().Be(TipoSemanticoColuna.NumericoQuantidade);
            pQtd.EhMetricaSugerida.Should().BeTrue();

            // 6. Texto Livre
            var pObs = perfis.First(p => p.Nome == "Observacoes");
            pObs.TipoSemantico.Should().Be(TipoSemanticoColuna.TextoLivre);
            pObs.EhDimensaoSugerida.Should().BeFalse();
            pObs.EhMetricaSugerida.Should().BeFalse();
        }

        [Fact]
        public void AnalisarColunas_ComColunaDeDadoPessoal_DeveAnonimizarExemplosPorLGPD()
        {
            // Arrange
            var headers = new[] { "CPF_Cliente", "Email", "Telefone" };
            var rows = new List<dynamic>();

            for (int i = 0; i < 10; i++)
            {
                dynamic r = new ExpandoObject();
                var d = (IDictionary<string, object>)r;
                d["CPF_Cliente"] = $"123.456.789-0{i}";
                d["Email"] = $"usuario{i}@teste.com";
                d["Telefone"] = $"11 98765-432{i}";
                rows.Add(r);
            }

            var dados = new DadosArquivo.RDadosArquivo(headers, rows);

            // Act
            var perfis = _sut.AnalisarColunas(dados);

            // Assert
            foreach (var p in perfis)
            {
                p.ExemplosAmostra.Should().ContainSingle().Which.Should().Be("[Anonimizado LGPD]");
            }
        }

        [Fact]
        public void AnalisarColunas_ComPlanilhaPequena_DeveProcessarComSucesso()
        {
            // Arrange
            var headers = new[] { "Produto", "Preco" };
            var rows = new List<dynamic>();

            for (int i = 0; i < 50; i++)
            {
                dynamic r = new ExpandoObject();
                var d = (IDictionary<string, object>)r;
                d["Produto"] = $"Item_{i % 5}";
                d["Preco"] = $"10.{i:D2}";
                rows.Add(r);
            }

            var dados = new DadosArquivo.RDadosArquivo(headers, rows);

            // Act
            var perfis = _sut.AnalisarColunas(dados);

            // Assert
            perfis.Should().HaveCount(2);
            perfis[0].CardinalidadeAmostra.Should().Be(5);
        }

        [Fact]
        public void AnalisarColunas_ComBaseMaiorQueDezMilLinhas_DeveProcessarRapidamenteEAplicarAmostragem()
        {
            // Arrange - 15.000 linhas
            var headers = new[] { "ID", "Categoria", "Valor" };
            var rows = new List<dynamic>(15000);

            var categorias = new[] { "Eletrônicos", "Moda", "Alimentos", "Serviços" };

            for (int i = 0; i < 15000; i++)
            {
                dynamic r = new ExpandoObject();
                var d = (IDictionary<string, object>)r;
                d["ID"] = i.ToString();
                d["Categoria"] = categorias[i % categorias.Length];
                d["Valor"] = $"R$ {(i % 500) + 10},00";
                rows.Add(r);
            }

            var dados = new DadosArquivo.RDadosArquivo(headers, rows);

            // Act
            var perfis = _sut.AnalisarColunas(dados);

            // Assert
            perfis.Should().HaveCount(3);
            var pCat = perfis.First(p => p.Nome == "Categoria");
            pCat.TipoSemantico.Should().Be(TipoSemanticoColuna.TextoCategoria);
            pCat.CardinalidadeAmostra.Should().Be(4);

            var pValor = perfis.First(p => p.Nome == "Valor");
            pValor.TipoSemantico.Should().Be(TipoSemanticoColuna.NumericoMonetario);
            pValor.EhMetricaSugerida.Should().BeTrue();
        }

        [Fact]
        public void AnalisarColunas_ComPlanilhaVaziaOuNula_DeveRetornarListaVaziaOuPerfisZerados()
        {
            // Arrange
            var dadosVazios = new DadosArquivo.RDadosArquivo(Array.Empty<string>(), new List<dynamic>());

            // Act
            var perfis = _sut.AnalisarColunas(dadosVazios);

            // Assert
            perfis.Should().BeEmpty();
        }
    }
}
