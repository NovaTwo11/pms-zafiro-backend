# ETAPA 1: Build
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copiamos los csproj de cada capa para restaurar dependencias
# Ajusta los nombres si difieren, pero basándome en tu estructura estándar:
COPY ["src/API/PmsZafiro.API.csproj", "src/API/"]
COPY ["src/Application/PmsZafiro.Application.csproj", "src/Application/"]
COPY ["src/Domain/PmsZafiro.Domain.csproj", "src/Domain/"]
COPY ["src/Infrastructure/PmsZafiro.Infrastructure.csproj", "src/Infrastructure/"]

# Restauramos dependencias (esto se cacheará si no cambian los csproj)
RUN dotnet restore "src/API/PmsZafiro.API.csproj"

# Copiamos todo el código fuente
COPY . .

# Compilamos y publicamos en modo Release
WORKDIR "/src/src/API"
RUN dotnet publish "PmsZafiro.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

# ETAPA 2: Runtime (Imagen final ligera)
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

# Copiamos los artefactos construidos en la etapa anterior
COPY --from=build /app/publish .

# Definimos el punto de entrada
ENTRYPOINT ["dotnet", "PmsZafiro.API.dll"]