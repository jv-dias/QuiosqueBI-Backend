// DENTRO DE Data/ApplicationDbContext.cs

using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using QuiosqueBI.API.Models;

namespace QuiosqueBI.API.Data
{
    public class ApplicationDbContext : IdentityDbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
        }

        // Cada DbSet<T> representa uma tabela que será criada no banco de dados.
        public DbSet<AnaliseSalva> AnalisesSalvas { get; set; }
    }
}