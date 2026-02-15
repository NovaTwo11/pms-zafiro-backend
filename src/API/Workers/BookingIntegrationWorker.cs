using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PmsZafiro.Application.Integrations.Booking;
using PmsZafiro.Domain.Entities;
using PmsZafiro.Domain.Enums;
using PmsZafiro.Infrastructure.Persistence;

namespace PmsZafiro.API.Workers;

public class BookingIntegrationWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<BookingIntegrationWorker> _logger;

    public BookingIntegrationWorker(IServiceScopeFactory scopeFactory, ILogger<BookingIntegrationWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Booking Integration Worker iniciado.");

        // Bucle infinito que se ejecuta cada 10 segundos
        while (!stoppingToken.IsCancellationRequested)
        {
            await ProcessInboxEventsAsync(stoppingToken);
            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
        }
    }

    private async Task ProcessInboxEventsAsync(CancellationToken stoppingToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<PmsDbContext>();

        // Buscar hasta 50 eventos pendientes por ciclo
        var pendingEvents = await context.IntegrationInboundEvents
            .Where(e => !e.IsProcessed && e.Channel == BookingChannel.BookingCom)
            .OrderBy(e => e.ReceivedAt)
            .Take(50)
            .ToListAsync(stoppingToken);

        if (!pendingEvents.Any()) return;

        foreach (var evt in pendingEvents)
        {
            try
            {
                // 1. Deserializar
                var payload = JsonSerializer.Deserialize<BookingPayloadDto>(evt.Payload);
                if (payload == null) throw new Exception("Payload JSON inválido o nulo.");

                // 2. Buscar el Mapping (La inteligencia de la integración)
                var mapping = await context.ChannelRoomMappings
                    .FirstOrDefaultAsync(m => 
                        m.Channel == BookingChannel.BookingCom && 
                        m.ExternalRoomId == payload.RoomData.ExternalRoomId, 
                        stoppingToken);

                if (mapping == null)
                {
                    throw new Exception($"No se encontró mapeo para la habitación de Booking ID: {payload.RoomData.ExternalRoomId}. Configúralo en el panel de PmsZafiro.");
                }

                // 3. Crear o buscar Huésped
                var guest = await context.Guests.FirstOrDefaultAsync(g => g.AliasEmail == payload.Guest.AliasEmail, stoppingToken);
                if (guest == null)
                {
                    guest = new Guest
                    {
                        FirstName = payload.Guest.FirstName,
                        LastName = payload.Guest.LastName,
                        AliasEmail = payload.Guest.AliasEmail,
                        DocumentType = IdType.PA, // Default para extranjeros, se ajusta en Check-In real
                        Email = payload.Guest.AliasEmail // Guardamos temporalmente el alias aquí también
                    };
                    context.Guests.Add(guest);
                }

                // 4. Crear Reserva
                var reservation = new Reservation
                {
                    ConfirmationCode = $"BKG-{new Random().Next(1000, 9999)}", // Tu logica interna de código
                    Guest = guest,
                    CheckIn = payload.CheckIn,
                    CheckOut = payload.CheckOut,
                    TotalAmount = payload.TotalAmount,
                    Channel = BookingChannel.BookingCom,
                    ExternalReservationId = payload.ExternalReservationId,
                    Status = ReservationStatus.Confirmed,
                    Adults = 2 // Esto también debería venir en el payload real
                };

                // Asignar a la categoría mapeada (A nivel de dominio, tu lógica puede requerir crear un ReservationSegment aquí)
                // reservation.Segments.Add(new ReservationSegment { ... });

                context.Reservations.Add(reservation);

                // 5. Marcar como procesado
                evt.IsProcessed = true;
                _logger.LogInformation($"Reserva externa {payload.ExternalReservationId} procesada con éxito.");
            }
            catch (Exception ex)
            {
                evt.ErrorMessage = ex.Message;
                _logger.LogError($"Error procesando evento {evt.Id}: {ex.Message}");
            }
        }

        await context.SaveChangesAsync(stoppingToken);
    }
}