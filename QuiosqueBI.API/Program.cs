using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using QuiosqueBI.API.Data;
using QuiosqueBI.API.Models; // Adicionado para garantir que ApplicationUser (se usado) seja encontrado
using QuiosqueBI.API.Services;


try{
var builder = WebApplication.CreateBuilder(args);

// --- SEÇÃO DE SERVIÇOS ---
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddScoped<IAnaliseService, AnaliseService>();

// Configuração do CORS (Simplificada para Robustez)
var myAllowSpecificOrigins = "_myAllowSpecificOrigins";
builder.Services.AddCors(options =>
{
    options.AddPolicy(name: myAllowSpecificOrigins,
        policy =>
        {
            policy.WithOrigins(
                "http://localhost:5173",
                "https://localhost:5173",
                "https://victorious-dune-05e42d21e.1.azurestaticapps.net" 
            )
            .AllowAnyHeader()
            .AllowAnyMethod();
        });
});

// Configuração do Entity Framework Core
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString));

// Configuração do Identity
builder.Services.AddIdentity<IdentityUser, IdentityRole>(options =>
    {
        options.Password.RequireDigit = false;
        options.Password.RequireLowercase = false;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequireUppercase = false;
        options.Password.RequiredLength = 3;
        options.User.AllowedUserNameCharacters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._@+ áéíóúàâêôãõç";
        options.User.RequireUniqueEmail = true;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

// Configuração do JWT Authentication
builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.SaveToken = true;
        options.RequireHttpsMetadata = true; // Alterado para true, que é o ideal para produção
        options.TokenValidationParameters = new TokenValidationParameters()
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidAudience = builder.Configuration["Jwt:Audience"],
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:SecretKey"]!))
        };
    });

var app = builder.Build();

// --- SEÇÃO DE MIDDLEWARE E LÓGICA DE INICIALIZAÇÃO ---

// Habilitar Swagger em todos os ambientes para facilitar testes no Azure
app.UseSwagger();
app.UseSwaggerUI();

//app.UseHttpsRedirection(); 

app.UseCors(myAllowSpecificOrigins);

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapGet("/health", () => Results.Ok("Healthy")); 

// Lógica de Migração e Seeding de Roles
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    dbContext.Database.Migrate();

    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    string[] roleNames = { "Admin", "Testador", "Usuario" };
    foreach (var roleName in roleNames)
    {
        if (!await roleManager.RoleExistsAsync(roleName))
        {
            await roleManager.CreateAsync(new IdentityRole(roleName));
        }
    }
}

app.Run();
}

catch (Exception ex)
{
    // Se QUALQUER exceção ocorrer durante a inicialização,
    // nós criamos uma mini-API de emergência para exibir o erro.
    var fallbackBuilder = WebApplication.CreateBuilder(args);
    var fallbackApp = fallbackBuilder.Build();
    fallbackApp.MapGet("/", () => Results.Problem(
        detail: ex.ToString(), // O stack trace completo da exceção
        title: "Erro Crítico na Inicialização da API"
    ));
    fallbackApp.Run();
}