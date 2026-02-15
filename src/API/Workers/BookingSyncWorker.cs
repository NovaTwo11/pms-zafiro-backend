using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PmsZafiro.Domain.Entities;
using PmsZafiro.Domain.Enums;
using PmsZafiro.Infrastructure.Persistence;

namespace PmsZafiro.API.Workers;

public class BookingSyncWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<BookingSyncWorker> _logger;
    private readonly IHttpClientFactory _httpClientFactory;

    public BookingSyncWorker(
        IServiceScopeFactory scopeFactory, 
        ILogger<BookingSyncWorker> logger,
        IHttpClientFactory httpClientFactory)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _httpClientFactory = httpClientFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("BookingSyncWorker iniciado.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPendingEventsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error crítico en el ciclo de BookingSyncWorker");
            }

            // Esperar 10 segundos antes del siguiente ciclo
            await Task.Delay(10000, stoppingToken);
        }
    }

    private async Task ProcessPendingEventsAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<PmsDbContext>();

        // Obtener eventos pendientes (máximo 10 por ciclo para no saturar)
        var events = await context.Set<IntegrationOutboundEvent>()
            .Where(e => e.Status == IntegrationStatus.Pending)
            .OrderBy(e => e.CreatedAt)
            .Take(10)
            .ToListAsync(ct);

        if (!events.Any()) return;

        foreach (var evt in events)
        {
            try
            {
                if (evt.EventType == IntegrationEventType.AvailabilityUpdate)
                {
                    await SyncAvailabilityToBookingAsync(evt, context, ct);
                }

                evt.Status = IntegrationStatus.Processed;
                evt.ProcessedAt = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fallo al sincronizar evento {Id}", evt.Id);
                evt.Status = IntegrationStatus.Failed;
                evt.ErrorMessage = ex.Message;
                evt.RetryCount++;
            }
        }

        await context.SaveChangesAsync(ct);
    }

    private async Task SyncAvailabilityToBookingAsync(IntegrationOutboundEvent evt, PmsDbContext context, CancellationToken ct)
    {
        // 1. Deserializar Payload
        var payload = JsonSerializer.Deserialize<JsonElement>(evt.Payload);
        
        // Obtenemos el string directo (Ej: "Doble")
        var categoryString = payload.GetProperty("InternalCategory").GetString();
        var startDate = payload.GetProperty("StartDate").GetDateTime();
        var endDate = payload.GetProperty("EndDate").GetDateTime();

        if (string.IsNullOrEmpty(categoryString))
            throw new Exception("Categoría interna no especificada en el evento.");

        // 2. Obtener Mapeo (CORREGIDO)
        // Usamos 'RoomCategory' (string) y 'BookingChannel.BookingCom' (Enum)
        var mapping = await context.Set<ChannelRoomMapping>()
            .FirstOrDefaultAsync(m => m.RoomCategory == categoryString && m.Channel == BookingChannel.BookingCom, ct);

        if (mapping == null)
        {
            _logger.LogWarning($"No existe mapeo para la categoría '{categoryString}' en Booking. Omitiendo.");
            return; 
        }

        // 3. CALCULAR INVENTARIO REAL
        // Paso A: Contar cuántas habitaciones físicas existen de esa categoría (String comparison)
        int totalPhysicalRooms = await context.Rooms
            .CountAsync(r => r.Category == categoryString && r.Status != RoomStatus.Maintenance, ct);

        // Paso B: Bucle día a día
        var currentDate = DateOnly.FromDateTime(startDate);
        var finalDate = DateOnly.FromDateTime(endDate);

        // Simulación del cliente (Aquí inyectarías IBookingService)
        // var client = _httpClientFactory.CreateClient("BookingClient");

        while (currentDate <= finalDate)
        {
            // Contar segmentos que ocupan esta noche específica para esta categoría
            int occupiedCount = await context.ReservationSegments
                .CountAsync(s => 
                    s.Room.Category == categoryString && // Match por string
                    s.CheckIn <= currentDate.ToDateTime(TimeOnly.MinValue) && 
                    s.CheckOut > currentDate.ToDateTime(TimeOnly.MinValue) && 
                    s.Reservation.Status != ReservationStatus.Cancelled, 
                    ct);

            int availability = totalPhysicalRooms - occupiedCount;
            if (availability < 0) availability = 0;

            // 4. Construir Payload para Booking (ARI)
            // Usamos mapping.ExternalRoomId que obtuvimos correctamente arriba
            var bookingPayload = new
            {
                id = mapping.ExternalRoomId,
                date = currentDate.ToString("yyyy-MM-dd"),
                rooms_to_sell = availability
            };

            // Log para verificar que funciona
            _logger.LogInformation($"[Booking Sync] Fecha: {currentDate:yyyy-MM-dd} | Cat: {categoryString} -> ExtID: {mapping.ExternalRoomId} | Disp: {availability}/{totalPhysicalRooms}");

            // TODO: Descomentar para producción cuando tengas las credenciales reales
            /*
            var response = await client.PostAsJsonAsync("/availability", bookingPayload, ct);
            if (!response.IsSuccessStatusCode) 
            {
                // Opcional: Leer el error de Booking para depurar
                var errorContent = await response.Content.ReadAsStringAsync();
                throw new Exception($"Error API Booking ({response.StatusCode}): {errorContent}");
            }
            */

            currentDate = currentDate.AddDays(1);
        }
    }
}