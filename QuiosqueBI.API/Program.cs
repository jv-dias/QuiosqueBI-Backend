// VERSÃO DE TESTE SIMPLIFICADA DO Program.cs

var builder = WebApplication.CreateBuilder(args);

// A única coisa que vamos registrar é o básico para os controllers funcionarem.
builder.Services.AddControllers();

// Comente temporariamente todas as outras configurações de serviço
// builder.Services.AddDbContext...
// builder.Services.AddIdentity...
// builder.Services.AddAuthentication...
// builder.Services.AddCors...
// builder.Services.AddScoped...

var app = builder.Build();

// Comente a lógica de seeding de roles
// using (var scope = ...){ ... }

// Adicionamos um endpoint de teste na raiz para ver se a API responde
app.MapGet("/", () => "API QuiosqueBI - Teste Online");

// Mantenha apenas o essencial para o pipeline
app.UseHttpsRedirection();
app.MapControllers();
app.Run();