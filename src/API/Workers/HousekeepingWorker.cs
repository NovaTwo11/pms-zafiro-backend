using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PmsZafiro.Infrastructure.Persistence;
using PmsZafiro.Domain.Enums;
using PmsZafiro.Domain.Entities;
using Microsoft.EntityFrameworkCore;

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

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                // 1. Usar SIEMPRE la hora de Colombia (UTC-5) para evitar fallos del servidor VPS
                var nowCol = DateTime.UtcNow.AddHours(-5);
                var nextRunCol = nowCol.Date.AddHours(6); // 6:00 AM de hoy
                
                if (nowCol >= nextRunCol) 
                {
                    nextRunCol = nextRunCol.AddDays(1); // Si ya pasaron las 6 AM, programar para mañana
                }
                
                var delay = nextRunCol - nowCol;

                _logger.LogInformation($"Próxima limpieza automática en: {delay.TotalHours:F2} horas ({nextRunCol:dd/MM HH:mm} - Hora Colombia)");

                // Esperar hasta las 6:00 AM exactas
                await Task.Delay(delay, stoppingToken);

                // 2. Ejecutar la marcación de limpieza
                using (var scope = _serviceProvider.CreateScope())
                {
                    var context = scope.ServiceProvider.GetRequiredService<PmsDbContext>();

                    // Buscar TODAS las habitaciones que tengan huéspedes en casa en este momento
                    var activeSegments = await context.Set<ReservationSegment>()
                        .Include(s => s.Room)
                        .Include(s => s.Reservation)
                        .Where(s => s.Reservation.Status == ReservationStatus.CheckedIn &&
                                    s.CheckIn <= DateTime.UtcNow &&
                                    s.CheckOut >= DateTime.UtcNow.AddMinutes(-10)) // Margen de seguridad
                        .ToListAsync(stoppingToken);

                    int updatedCount = 0;
                    foreach (var segment in activeSegments)
                    {
                        // Si la habitación no está en mantenimiento y no está sucia, la marcamos
                        if (segment.Room != null && segment.Room.Status != RoomStatus.Maintenance && segment.Room.Status != RoomStatus.Dirty)
                        {
                            segment.Room.Status = RoomStatus.Dirty;
                            updatedCount++;
                            _logger.LogInformation($"Habitación {segment.Room.Number} marcada SUCIA automáticamente (Huésped en casa).");
                        }
                    }

                    if (updatedCount > 0)
                    {
                        await context.SaveChangesAsync(stoppingToken);
                        _logger.LogInformation($"[OK] Se marcaron {updatedCount} habitaciones como sucias a las 6:00 AM.");
                    }
                }
            }
        }
        catch (TaskCanceledException)
        {
            _logger.LogInformation("Housekeeping Worker detenido correctamente.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error crítico en Housekeeping Worker");
        }
    }
}