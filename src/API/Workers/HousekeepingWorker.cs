using PmsZafiro.Application.Interfaces;
using PmsZafiro.Domain.Enums;

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
        _logger.LogInformation("🧹 Housekeeping Worker iniciado. Programado para las 06:00 AM.");

        while (!stoppingToken.IsCancellationRequested)
        {
            // 1. Calcular cuánto falta para las próximas 6:00 AM
            var now = DateTime.Now;
            var nextRun = now.Date.AddHours(6);
            if (now >= nextRun) nextRun = nextRun.AddDays(1);

            var delay = nextRun - now;
            _logger.LogInformation($"Dormiré durante {delay.TotalHours:N2} horas hasta el próximo ciclo de limpieza.");

            // 2. Esperar
            await Task.Delay(delay, stoppingToken);

            // 3. ¡Ejecutar la magia!
            await SetAllCleanRoomsToDirty();
        }
    }

    private async Task SetAllCleanRoomsToDirty()
    {
        using (var scope = _serviceProvider.CreateScope())
        {
            var roomRepo = scope.ServiceProvider.GetRequiredService<IRoomRepository>();
            var rooms = await roomRepo.GetAllAsync();

            int count = 0;
            foreach (var room in rooms)
            {
                // REGLA: Si está Limpia (Available) -> Pasa a Sucia (Dirty)
                // Ignoramos las que ya están Ocupadas, Mantenimiento o Bloqueadas
                if (room.Status == RoomStatus.Available) 
                {
                    room.Status = RoomStatus.Dirty;
                    await roomRepo.UpdateAsync(room);
                    count++;
                }
            }
            _logger.LogInformation($"✅ AUTO-LIMPIEZA: Se marcaron {count} habitaciones como Sucias.");
        }
    }
}