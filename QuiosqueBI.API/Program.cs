using System.Text;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using QuiosqueBI.API.Data;
using QuiosqueBI.API.Models;
using QuiosqueBI.API.Common.Behaviors;

try {
    var builder = WebApplication.CreateBuilder(args);
    var env = builder.Environment;

    // --- SEÇÃO DE SERVIÇOS ---
    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();
    
    // Registrar MediatR
    builder.Services.AddMediatR(cfg => 
    {
        cfg.RegisterServicesFromAssembly(typeof(Program).Assembly);
    });
    builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(QuiosqueBI.API.Common.Behaviors.PerformanceBehavior<,>));
    builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(QuiosqueBI.API.Common.Behaviors.ValidationBehavior<,>));

    // Configuração CORS melhorada para trabalhar com Azure e ambiente de desenvolvimento
    builder.Services.AddCors(options =>
    {
        options.AddDefaultPolicy(builder =>
        {
            builder.AllowAnyOrigin()
                   .AllowAnyMethod()
                   .AllowAnyHeader()
                   .WithExposedHeaders("Content-Disposition");
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

    // Habilitar Swagger em todos os ambientes
    app.UseSwagger();
    app.UseSwaggerUI();

    // IMPORTANTE: CORS precisa vir ANTES do UseRouting e UseAuthentication
    app.UseCors();

    // Registrar um middleware para diagnosticar requisições 404
    app.Use(async (context, next) =>
    {
        await next();
        
        // Se chegamos aqui com 404, logar detalhes para diagnóstico
        if (context.Response.StatusCode == 404)
        {
            Console.WriteLine($"404 Não Encontrado: {context.Request.Method} {context.Request.Path}");
            
            // Se for uma requisição OPTIONS (preflight CORS), retorna 200 com cabeçalhos CORS
            if (context.Request.Method == "OPTIONS")
            {
                context.Response.StatusCode = 200;
                context.Response.Headers.Add("Access-Control-Allow-Origin", "*");
                context.Response.Headers.Add("Access-Control-Allow-Methods", "GET, POST, PUT, DELETE, OPTIONS");
                context.Response.Headers.Add("Access-Control-Allow-Headers", "Content-Type, Authorization");
            }
        }
    });

    app.UseRouting();
    app.UseAuthentication();
    app.UseAuthorization();

    app.MapControllers();
    app.MapGet("/", () => "API QuiosqueBI funcionando!");
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