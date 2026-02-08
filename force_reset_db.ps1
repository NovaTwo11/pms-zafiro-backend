# reset_db_v2.ps1
$ErrorActionPreference = "Stop"

Write-Host "--- DETENIENDO PROCESOS ---" -ForegroundColor Yellow
# Intentamos matar cualquier proceso dotnet que este bloqueando la DB
try {
    Stop-Process -Name "dotnet" -Force -ErrorAction SilentlyContinue
    Stop-Process -Name "PmsZafiro.API" -Force -ErrorAction SilentlyContinue
} catch {
    # Ignorar errores si no hay procesos
}

Write-Host "--- LIMPIANDO BASE DE DATOS ---" -ForegroundColor Cyan

# 1. Borrar carpeta de Migraciones
$migrationsPath = "src/Infrastructure/Migrations"
if (Test-Path $migrationsPath) {
    Remove-Item -Path $migrationsPath -Recurse -Force
    Write-Host "OK: Carpeta Migrations eliminada." -ForegroundColor Green
}

# 2. Borrar la Base de Datos
Write-Host "Intentando borrar la base de datos..." -ForegroundColor Yellow
try {
    dotnet ef database drop -f -p src/Infrastructure -s src/API
} catch {
    Write-Host "ERROR: No se pudo borrar la DB. Es posible que SQL Server la tenga bloqueada." -ForegroundColor Red
    Write-Host "INTENTO DE SOLUCION: Cierra tu IDE o reinicia el servicio de SQL Server si esto persiste." -ForegroundColor Red
    exit
}

# 3. Crear Nueva Migracion
Write-Host "--- CREANDO NUEVO ESQUEMA ---" -ForegroundColor Cyan
dotnet ef migrations add InitialSchema -p src/Infrastructure -s src/API

if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: Fallo al crear la migracion. Revisa errores de compilacion." -ForegroundColor Red
    exit
}

# 4. Actualizar Base de Datos
Write-Host "--- APLICANDO CAMBIOS A SQL SERVER ---" -ForegroundColor Cyan
dotnet ef database update -p src/Infrastructure -s src/API

Write-Host " "
Write-Host "=== LISTO ===" -ForegroundColor Green
Write-Host "La base de datos se ha reiniciado correctamente."
Write-Host "Ahora puedes ejecutar: dotnet run --project src/API"