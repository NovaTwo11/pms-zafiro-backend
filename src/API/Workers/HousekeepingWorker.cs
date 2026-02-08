using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PmsZafiro.Infrastructure.Persistence;
using PmsZafiro.Domain.Enums;
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
                // Calcular tiempo hasta las 6:00 AM
                var now = DateTime.Now;
                var nextRun = now.Date.AddHours(6);
                if (now > nextRun) nextRun = nextRun.AddDays(1);
                var delay = nextRun - now;

                _logger.LogInformation($"Próxima limpieza automática en: {delay.TotalHours:F2} horas ({nextRun:dd/MM HH:mm})");

                // Esperar (Si se cancela aquí, lanza OperationCanceledException)
                await Task.Delay(delay, stoppingToken);

                // Ejecutar la tarea
                using (var scope = _serviceProvider.CreateScope())
                {
                    var context = scope.ServiceProvider.GetRequiredService<PmsDbContext>();
                    var today = DateTime.UtcNow.Date;

                    var reservationsEndingToday = await context.Reservations
                        .Include(r => r.Room)
                        .Where(r => r.Status == ReservationStatus.CheckedIn && r.CheckOut.Date <= today)
                        .ToListAsync(stoppingToken);

                    foreach (var res in reservationsEndingToday)
                    {
                        if (res.Room != null && res.Room.Status != RoomStatus.Dirty)
                        {
                            res.Room.Status = RoomStatus.Dirty;
                            _logger.LogInformation($"Habitación {res.Room.Number} marcada SUCIA (Check-out vencido).");
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
            // Este catch evita el mensaje de error "crítico" al detener el servidor con Ctrl+C
            _logger.LogInformation("Housekeeping Worker detenido correctamente.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error crítico en Housekeeping Worker");
        }
    }
}