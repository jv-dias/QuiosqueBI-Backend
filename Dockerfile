# Estágio 1: Build
# Usamos a imagem do SDK do .NET 8 para compilar o projeto.
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copia o arquivo .csproj e restaura as dependências primeiro.
# Isso aproveita o cache do Docker: se as dependências não mudaram, ele não baixa tudo de novo.
COPY ["QuiosqueBI.API/QuiosqueBI.API.csproj", "QuiosqueBI.API/"]
RUN dotnet restore "QuiosqueBI.API/QuiosqueBI.API.csproj"

# Copia todo o resto do código do projeto
COPY . .
WORKDIR "/src/QuiosqueBI.API"
# Compila e publica o projeto em modo Release, otimizado para produção.
RUN dotnet publish "QuiosqueBI.API.csproj" -c Release -o /app/publish

# Estágio 2: Final
# Agora, usamos uma imagem muito menor, que contém apenas o necessário para RODAR a aplicação, não para compilar.
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Copia apenas os arquivos publicados do estágio de build para a imagem final.
COPY --from=build /app/publish .

# Define o comando que será executado quando o contêiner iniciar.
ENTRYPOINT ["dotnet", "QuiosqueBI.API.dll"]

# Usa uma variável de ambiente para a URL, o que é uma boa prática
ENV ASPNETCORE_URLS=http://+:8080

# Exponha a porta para que ela possa ser mapeada para o host
EXPOSE 8080
