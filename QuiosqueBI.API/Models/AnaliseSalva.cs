// DENTRO DE Models/AnaliseSalva.cs
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Identity;

namespace QuiosqueBI.API.Models
{
    /// <summary>
    /// É o modelo que representa uma análise salva no banco de dados.
    /// Contém informações sobre o contexto da análise, a data de criação,
    /// os resultados em formato JSON e a referência ao usuário que a criou.
    /// </summary>
    public class AnaliseSalva
    {
        [Key] 
        public int Id { get; set; }

        [Required] 
        public string Contexto { get; set; } = string.Empty;

        public DateTime DataCriacao { get; set; }
        
        [Column(TypeName = "jsonb")]
        public string ResultadosJson { get; set; } = string.Empty;
        
        [Required]
        public string? UserId { get; set; } 

        [ForeignKey("UserId")]
        public virtual IdentityUser? User { get; set; } // Propriedade de navegação
    }
}