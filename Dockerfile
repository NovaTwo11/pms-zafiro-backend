# ETAPA 1: Build
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copiamos los csproj de cada capa para restaurar dependencias
COPY ["src/API/PmsZafiro.API.csproj", "src/API/"]
COPY ["src/Application/PmsZafiro.Application.csproj", "src/Application/"]
COPY ["src/Domain/PmsZafiro.Domain.csproj", "src/Domain/"]
COPY ["src/Infrastructure/PmsZafiro.Infrastructure.csproj", "src/Infrastructure/"]

# Restauramos dependencias
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

# --- CORRECCIÓN CRÍTICA ---
# Instalamos la librería de autenticación requerida por el driver de base de datos
USER root
RUN apt-get update && \
    apt-get install -y libgssapi-krb5-2 && \
    rm -rf /var/lib/apt/lists/*
# --------------------------

# Copiamos los artefactos construidos en la etapa anterior
COPY --from=build /app/publish .

# Definimos el punto de entrada
ENTRYPOINT ["dotnet", "PmsZafiro.API.dll"]