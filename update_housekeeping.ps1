# Script para actualizar el Worker de Limpieza Automática (6 AM)

$basePath = "src/API/Workers"
$filePath = "$basePath/HousekeepingWorker.cs"

# Asegurar que el directorio existe
if (-not (Test-Path $basePath)) {
    New-Item -ItemType Directory -Force -Path $basePath | Out-Null
}

$content = @"
using Microsoft.EntityFrameworkCore;
using PmsZafiro.Domain.Enums;
using PmsZafiro.Infrastructure.Persistence;

namespace PmsZafiro.API.Workers;

public class HousekeepingWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<HousekeepingWorker> _logger;

    public HousekeepingWorker(IServiceProvider serviceProvider, ILogger<HousekeepingWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Housekeeping Worker iniciado.");

        while (!stoppingToken.IsCancellationRequested)
        {
            var now = DateTime.Now;
            
            // Configuración: Ejecutar a las 6:00 AM
            var nextRun = now.Date.AddHours(6);
            
            // Si ya pasaron las 6 AM de hoy, programar para mañana
            if (now >= nextRun)
            {
                nextRun = nextRun.AddDays(1);
            }

            var delay = nextRun - now;
            _logger.LogInformation($"Próxima ejecución de limpieza automática en: {delay.TotalHours:N2} horas ({nextRun:dd/MM/yyyy HH:mm})");

            // Esperar hasta la hora programada
            try 
            {
                await Task.Delay(delay, stoppingToken);
            }
            catch (TaskCanceledException)
            {
                break;
            }

            try
            {
                await RunDailyHousekeepingLogic();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error ejecutando lógica de limpieza diaria");
            }
        }
    }

    private async Task RunDailyHousekeepingLogic()
    {
        using (var scope = _serviceProvider.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<PmsDbContext>();
            
            _logger.LogInformation("Ejecutando reglas de limpieza diaria (6:00 AM)...");

            // --- REGLA 1: LIMPIEZA DIARIA DE HABITACIONES OCUPADAS ---
            // Busca habitaciones que están actualmente marcadas como OCUPADAS.
            // Las pasa a estado SUCIA para indicar que requieren servicio de camarera.
            // Nota: El frontend debe saber que si hay una reserva activa, 'Sucia' significa 'Ocupada/Sucia'.
            
            var occupiedRooms = await context.Rooms
                .Where(r => r.Status == RoomStatus.Occupied)
                .ToListAsync();

            int updatedCount = 0;
            foreach (var room in occupiedRooms)
            {
                room.Status = RoomStatus.Dirty; 
                updatedCount++;
            }

            // --- REGLA 2: MANTENIMIENTOS VENCIDOS (Placeholder) ---
            // Aquí se puede agregar lógica para liberar habitaciones cuyo mantenimiento haya finalizado ayer.
            // var maintenanceFinished = await context.Rooms.Where(r => r.Status == RoomStatus.Maintenance && r.MaintenanceEndDate < DateTime.Now).ToListAsync();
            // foreach(var r in maintenanceFinished) r.Status = RoomStatus.Dirty; // Para limpieza post-mantenimiento

            if (updatedCount > 0)
            {
                await context.SaveChangesAsync();
                _logger.LogInformation($"Housekeeping: Se marcaron {updatedCount} habitaciones ocupadas como 'Sucias' para limpieza diaria.");
            }
            else
            {
                _logger.LogInformation("Housekeeping: No se encontraron habitaciones ocupadas para marcar.");
            }
        }
    }
}
"@

Set-Content -Path $filePath -Value $content -Force
Write-Host "✅ HousekeepingWorker.cs ha sido actualizado correctamente."
Write-Host "   Ubicación: $filePath"
Write-Host "   Lógica: Ejecución diaria a las 6:00 AM -> Habitaciones Ocupadas pasan a Sucias."