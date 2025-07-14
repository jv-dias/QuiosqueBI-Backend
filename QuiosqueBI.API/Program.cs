var builder = WebApplication.CreateBuilder(args);

// Adicionamos apenas os serviços mínimos para a API funcionar.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Endpoint de diagnóstico
app.MapGet("/debug-config", (IConfiguration config) =>
{
	// Tentamos ler todas as configurações que precisamos
	var connectionString = config.GetConnectionString("DefaultConnection");
	var connectionStringDirect = config["ConnectionStrings:DefaultConnection"];
	var connectionStringDoubleUnderscore = config["ConnectionStrings__DefaultConnection"];
    
	var geminiKey = config["Gemini:ApiKey"];
	var jwtKey = config["Jwt:SecretKey"];
	var jwtIssuer = config["Jwt:Issuer"];

	// Retornamos um objeto JSON com tudo que a aplicação conseguiu ler
	return Results.Ok(new
	{
		Message = "Valores de configuração lidos pelo aplicativo no Azure.",
		GetConnectionString_Result = string.IsNullOrEmpty(connectionString) ? "NULO OU VAZIO" : "ENCONTRADO",
		DirectRead_Result = string.IsNullOrEmpty(connectionStringDirect) ? "NULO OU VAZIO" : "ENCONTRADO",
		DoubleUnderscore_Result = string.IsNullOrEmpty(connectionStringDoubleUnderscore) ? "NULO OU VAZIO" : "ENCONTRADO",
		ActualConnectionStringValue_For_DoubleUnderscore = connectionStringDoubleUnderscore, // Mostra o valor real que ele leu
		GeminiKey_IsPresent = !string.IsNullOrEmpty(geminiKey),
		JwtKey_IsPresent = !string.IsNullOrEmpty(jwtKey),
		JwtIssuer_IsPresent = !string.IsNullOrEmpty(jwtIssuer)
	});
});

app.UseHttpsRedirection();
app.MapControllers();
app.Run();