# Estágio 1: Build da aplicação .NET 8
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copia arquivo do projeto e restaura dependências primeiro (aproveita cache do Docker)
COPY ["EncurtadorLink.csproj", "./"]
RUN dotnet restore "EncurtadorLink.csproj"

# Copia todos os arquivos restantes e compila em modo Release
COPY . .
RUN dotnet publish "EncurtadorLink.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Estágio 2: Imagem leve de execução
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

# Porta padrão de escuta
EXPOSE 8080

ENTRYPOINT ["dotnet", "EncurtadorLink.dll"]
