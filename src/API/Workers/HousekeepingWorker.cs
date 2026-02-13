using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PmsZafiro.Infrastructure.Persistence;
using PmsZafiro.Domain.Enums;
using PmsZafiro.Domain.Entities; // Necesario para ReservationSegment
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
                // Calcular tiempo hasta las 6:00 AM (Hora configurable idealmente)
                var now = DateTime.Now;
                var nextRun = now.Date.AddHours(6);
                if (now > nextRun) nextRun = nextRun.AddDays(1);
                var delay = nextRun - now;

                _logger.LogInformation($"Próxima limpieza automática en: {delay.TotalHours:F2} horas ({nextRun:dd/MM HH:mm})");

                // Esperar
                await Task.Delay(delay, stoppingToken);

                // Ejecutar la tarea de marcación automática
                using (var scope = _serviceProvider.CreateScope())
                {
                    var context = scope.ServiceProvider.GetRequiredService<PmsDbContext>();
                    var today = DateTime.UtcNow.Date;

                    // FIX: Incluir Segmentos -> Habitación en lugar de Habitación directa
                    var reservationsEndingToday = await context.Reservations
                        .Include(r => r.Segments)
                        .ThenInclude(s => s.Room)
                        .Where(r => r.Status == ReservationStatus.CheckedIn && r.CheckOut.Date <= today)
                        .ToListAsync(stoppingToken);

                    foreach (var res in reservationsEndingToday)
                    {
                        // Buscamos la habitación del último segmento (la que se está liberando)
                        var lastSegment = res.Segments
                            .OrderByDescending(s => s.CheckOut)
                            .FirstOrDefault();

                        if (lastSegment != null && lastSegment.Room != null && lastSegment.Room.Status != RoomStatus.Dirty)
                        {
                            lastSegment.Room.Status = RoomStatus.Dirty;
                            _logger.LogInformation($"Habitación {lastSegment.Room.Number} marcada SUCIA (Check-out vencido de reserva {res.ConfirmationCode}).");
                        }
                    }

                    if (reservationsEndingToday.Any())
                    {
                        await context.SaveChangesAsync(stoppingToken);
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Housekeeping Worker detenido correctamente.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error crítico en Housekeeping Worker");
        }
    }
}