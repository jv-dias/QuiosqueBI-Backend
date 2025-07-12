using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using QuiosqueBI.API.Data;
using QuiosqueBI.API.Services;

var builder = WebApplication.CreateBuilder(args);

// --- SEÇÃO 1: REATIVADA ---
// Configuração do CORS para permitir requisições do frontend
var myAllowSpecificOrigins = "_myAllowSpecificOrigins";
builder.Services.AddCors(options =>
{
    options.AddPolicy(name: myAllowSpecificOrigins,
        policy =>
        {
            // Adicione a URL do seu frontend de produção aqui também
            policy.WithOrigins("http://localhost:5173", "https://localhost:5173", "URL_DO_SEU_FRONTEND_NO_AZURE")
                  .AllowAnyHeader()
                  .AllowAnyMethod();
        });
});

// --- SEÇÃO 2: REATIVADA ---
// Configuração do Entity Framework Core com PostgreSQL
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString));

// --- SEÇÕES QUE PERMANECEM COMENTADAS POR ENQUANTO ---
/*
// Configuração do Identity
builder.Services.AddIdentity<IdentityUser, IdentityRole>(options =>
    {
        // ...
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

builder.Services.AddScoped<IAnaliseService, AnaliseService>();

// Configuração do JWT Authentication
builder.Services.AddAuthentication(options =>
    {
        // ...
    })
    .AddJwtBearer(options =>
    {
        // ...
    });
*/

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddControllers();

var app = builder.Build();

// --- LÓGICA DE SEEDING AINDA COMENTADA ---
/*
using (var scope = app.Services.CreateScope())
{
    // ...
}
*/

// Apenas o Swagger fica no ambiente de desenvolvimento
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors(myAllowSpecificOrigins); // <-- CORS REATIVADO

// Autenticação e Autorização ainda comentadas
// app.UseAuthentication();
// app.UseAuthorization();

app.MapControllers();

// Adicionamos nosso endpoint de teste para verificar se a API está online
app.MapGet("/", () => "API QuiosqueBI - Teste com DBContext Ativo");

app.Run();