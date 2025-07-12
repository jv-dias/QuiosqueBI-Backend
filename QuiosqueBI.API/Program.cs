using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using QuiosqueBI.API.Data;
using QuiosqueBI.API.Services;

var builder = WebApplication.CreateBuilder(args);

// --- SEÇÃO 1: REATIVADA ---
var myAllowSpecificOrigins = "_myAllowSpecificOrigins";
builder.Services.AddCors(options =>
{
    options.AddPolicy(name: myAllowSpecificOrigins,
        policy =>
        {
            policy.WithOrigins("http://localhost:5173", "https://localhost:5173", "https://victorious-dune-05e42d21e.1.azurestaticapps.net")
                  .AllowAnyHeader()
                  .AllowAnyMethod();
        });
});

// --- SEÇÃO 2: REATIVADA ---
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString));

// --- SEÇÃO 3: REATIVADA ---
// Configuração do Identity
builder.Services.AddIdentity<IdentityUser, IdentityRole>(options =>
    {
        options.Password.RequireDigit = false;
        options.Password.RequireLowercase = false;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequireUppercase = false;
        options.Password.RequiredLength = 3;
        options.User.AllowedUserNameCharacters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._@+ áéíóúàâêôãõç";
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

// --- SEÇÕES QUE PERMANECEM COMENTADAS ---
/*
builder.Services.AddScoped<IAnaliseService, AnaliseService>();

// Configuração do JWT Authentication
builder.Services.AddAuthentication(options => { ... })
    .AddJwtBearer(options => { ... });
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

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors(myAllowSpecificOrigins);

// --- MIDDLEWARES DE AUTENTICAÇÃO REATIVADOS ---
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Alteramos a mensagem de teste para refletir a etapa atual
app.MapGet("/", () => "API QuiosqueBI - Teste com DBContext e Identity Ativos");

app.Run();