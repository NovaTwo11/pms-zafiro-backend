# fix_swagger.ps1

# 1. Definir rutas
$CurrentLocation = Get-Location
$SrcPath = Join-Path $CurrentLocation "src"
$ApiPath = Join-Path $SrcPath "API"
$CsprojPath = Join-Path $ApiPath "PmsZafiro.API.csproj"
$ProgramCsPath = Join-Path $ApiPath "Program.cs"

if (-not (Test-Path $ApiPath)) {
    Write-Error "❌ No se encuentra la carpeta 'src/API'. Ejecuta esto en la raíz del proyecto."
    exit
}

# 2. Instalar Swashbuckle.AspNetCore (Gestor de Swagger UI)
Write-Host "📦 Instalando Swashbuckle.AspNetCore..." -ForegroundColor Cyan
dotnet add $CsprojPath package Swashbuckle.AspNetCore

# 3. Sobrescribir Program.cs con la configuración de Swagger
$ProgramCsContent = @"
using Microsoft.EntityFrameworkCore;
using PmsZafiro.Infrastructure.Persistence;
using PmsZafiro.Application.Interfaces;
using PmsZafiro.Application.Services;
using PmsZafiro.Infrastructure.Repositories;

var builder = WebApplication.CreateBuilder(args);

// --- Configuración DB ---
builder.Services.AddDbContext<PmsDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString(""DefaultConnection"")));

// --- Configuración CORS ---
builder.Services.AddCors(options =>
{
    options.AddPolicy(""AllowNextJs"", policy =>
    {
        policy.WithOrigins(""http://localhost:3000"")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

// --- Configuración SWAGGER (Reemplazando OpenAPI nativo) ---
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(); // Generador de Swagger

builder.Services.AddControllers();

// --- Inyección de Dependencias ---
builder.Services.AddScoped<IReservationRepository, ReservationRepository>();
builder.Services.AddScoped<IGuestRepository, GuestRepository>();
builder.Services.AddScoped<IRoomRepository, RoomRepository>();
builder.Services.AddScoped<IFolioRepository, FolioRepository>();
builder.Services.AddScoped<ICashierRepository, CashierRepository>();
builder.Services.AddScoped<CashierService>();
builder.Services.AddScoped<INotificationRepository, NotificationRepository>();

// Workers
builder.Services.AddHostedService<PmsZafiro.API.Workers.HousekeepingWorker>();

var app = builder.Build();

// --- Pipeline HTTP ---
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();   // Habilita el JSON
    app.UseSwaggerUI(); // Habilita la Interfaz Gráfica en /swagger
}

app.UseHttpsRedirection();
app.UseCors(""AllowNextJs"");

app.MapControllers(); 

app.Run();
"@

Set-Content -Path $ProgramCsPath -Value $ProgramCsContent
Write-Host "✅ Program.cs actualizado con Swagger UI." -ForegroundColor Green
Write-Host "🚀 Ejecuta 'dotnet run' y abre http://localhost:5100/swagger" -ForegroundColor Cyan