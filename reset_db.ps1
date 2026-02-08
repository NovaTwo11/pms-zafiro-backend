# reset_db.ps1
$ErrorActionPreference = "Stop"
Write-Host "🔥 INICIANDO REINICIO TOTAL DE BASE DE DATOS..." -ForegroundColor Red

# 1. Eliminar carpeta de Migraciones vieja
if (Test-Path "src/Infrastructure/Migrations") {
    Remove-Item -Path "src/Infrastructure/Migrations" -Recurse -Force
    Write-Host "🗑️ Carpeta de Migraciones eliminada." -ForegroundColor Yellow
}

# 2. Eliminar la BD actual (La más drástica pero efectiva solución en Dev)
Write-Host "🗑️ Eliminando base de datos antigua..." -ForegroundColor Yellow
dotnet ef database drop -f -p src/Infrastructure -s src/API

# 3. Crear nueva Migración limpia
Write-Host "📦 Creando nueva migración 'InitialSchema'..." -ForegroundColor Cyan
dotnet ef migrations add InitialSchema -p src/Infrastructure -s src/API

# 4. Aplicar a la BD
Write-Host "🚀 Creando base de datos nueva..." -ForegroundColor Green
dotnet ef database update -p src/Infrastructure -s src/API

Write-Host "✅ ¡BASE DE DATOS REPARADA Y SINCRONIZADA!" -ForegroundColor Green