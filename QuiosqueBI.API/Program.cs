using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using QuiosqueBI.API.Data;
using QuiosqueBI.API.Services;

var builder = WebApplication.CreateBuilder(args);

// Configuração do CORS para permitir requisições do frontend
var myAllowSpecificOrigins = "_myAllowSpecificOrigins";
builder.Services.AddCors(options =>
{
    options.AddPolicy(name: myAllowSpecificOrigins,
        policy =>
        {
            policy.WithOrigins(
                    "http://localhost:5173",                                  // URL do Vue em desenvolvimento
                    "http://localhost:4173",                                  // URL do Vue em preview
                    "http://localhost:3000",                                  // Outra porta comum de desenvolvimento
                    "http://localhost",                                       // Origem genérica
                    "https://victorious-dune-05e42d21e.1.azurestaticapps.net", // URL de produção (exemplo)
                    "https://*.azurestaticapps.net"                           // Qualquer URL do Azure Static Web Apps
                )
                .SetIsOriginAllowedToAllowWildcardSubdomains() // Permite subdomínios do Azure
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials(); // Permite enviar credenciais (cookies, etc)
        });
});

// Configuração do Entity Framework Core com PostgreSQL
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString));

// Configuração do Identity
builder.Services.AddIdentity<IdentityUser, IdentityRole>(options =>
    {
        // Configurações opcionais de senha para facilitar no ambiente de desenvolvimento
        options.Password.RequireDigit = false;
        options.Password.RequireLowercase = false;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequireUppercase = false;
        options.Password.RequiredLength = 3;
        options.Password.RequiredUniqueChars = 1;
        options.User.AllowedUserNameCharacters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._@+ áéíóúàâêôãõç";
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddControllers();
builder.Services.AddScoped<IAnaliseService, AnaliseService>();// Registro do serviço de análise
builder.Services.AddControllers();

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
        options.RequireHttpsMetadata = false; // Em produção, mudar para true.
        options.TokenValidationParameters = new TokenValidationParameters()
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidAudience = builder.Configuration["Jwt:Audience"],
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:SecretKey"]))
        };
    });


var app = builder.Build();

// Aplica as migrações pendentes ao banco de dados no início da aplicação.
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    dbContext.Database.Migrate();
}

using (var scope = app.Services.CreateScope())
{
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

    string[] roleNames = { "Admin", "Testador", "Usuario" };
    IdentityResult roleResult;

    foreach (var roleName in roleNames)
    {
        var roleExist = await roleManager.RoleExistsAsync(roleName);
        if (!roleExist)
        {
            // Cria a role se ela não existir
            roleResult = await roleManager.CreateAsync(new IdentityRole(roleName));
        }
    }
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
//app.UseHttpsRedirection(); // Força HTTPS

// Aplique o middleware CORS ANTES de UseAuthentication e UseAuthorization
app.UseCors(myAllowSpecificOrigins); 

app.UseAuthentication(); // <-- Primeiro, verifica quem é o usuário
app.UseAuthorization(); // <-- Depois, verifica o que ele pode fazer
app.MapControllers();
app.MapGet("/health", () => Results.Ok("Healthy"));	
app.Run();