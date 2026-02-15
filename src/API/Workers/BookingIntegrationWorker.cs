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

                // 2. Buscar el Mapping (Para saber qué Categoría compraron)
                var mapping = await context.ChannelRoomMappings
                    .FirstOrDefaultAsync(m => 
                        m.Channel == BookingChannel.BookingCom && 
                        m.ExternalRoomId == payload.RoomData.ExternalRoomId, 
                        stoppingToken);

                if (mapping == null)
                    throw new Exception($"No se encontró mapeo para la habitación de Booking ID: {payload.RoomData.ExternalRoomId}.");

                // --- LA MAGIA: 2.5 BUSCAR HABITACIÓN FÍSICA DISPONIBLE ---
                // (Nota de Arquitectura: Si tu IRoomRepository tiene un método "GetAvailableRoomByCategoryAsync", 
                // deberías inyectar el repo y llamarlo aquí. Si no, esta consulta EF Core es infalible).
                var availableRoom = await context.Rooms
                    .Where(r => r.Category == mapping.RoomCategory && r.Status == RoomStatus.Available) // O el estado activo que uses
                    .FirstOrDefaultAsync(r => !context.ReservationSegments.Any(seg =>
                        seg.RoomId == r.Id &&
                        seg.CheckIn < payload.CheckOut && // Lógica de solapamiento de fechas
                        seg.CheckOut > payload.CheckIn), stoppingToken);

                if (availableRoom == null)
                {
                    // ALERTA DE OVERBOOKING: Booking vendió una categoría que ya tienes llena.
                    // Aquí puedes lanzar un error, o en un PMS maduro, asignarla a una "Habitación Virtual" (Dummy Room).
                    throw new Exception($"Overbooking detectado: No hay habitaciones físicas disponibles en la categoría '{mapping.RoomCategory}' del {payload.CheckIn:dd/MM} al {payload.CheckOut:dd/MM}.");
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
                        DocumentType = IdType.PA, // Ajusta según tu Enum
                        Email = payload.Guest.AliasEmail
                    };
                    context.Guests.Add(guest);
                }

                // 4. Crear Reserva
                var reservation = new Reservation
                {
                    ConfirmationCode = $"BKG-{new Random().Next(10000, 99999)}",
                    Guest = guest,
                    CheckIn = payload.CheckIn,
                    CheckOut = payload.CheckOut,
                    TotalAmount = payload.TotalAmount,
                    Channel = BookingChannel.BookingCom,
                    ExternalReservationId = payload.ExternalReservationId,
                    Status = ReservationStatus.Confirmed,
                    Adults = 2
                };

                // --- AHORA SÍ: CREAMOS EL SEGMENTO USANDO LA HABITACIÓN FÍSICA ---
                reservation.Segments.Add(new ReservationSegment
                {
                    RoomId = availableRoom.Id, // <- La habitación que nuestro algoritmo encontró vacía
                    CheckIn = payload.CheckIn,
                    CheckOut = payload.CheckOut
                });

                // 5. Guardar en Base de Datos
                // (De nuevo, si prefieres usar _reservationRepository.AddAsync(reservation), 
                // puedes pedir el repo al _scopeFactory así: scope.ServiceProvider.GetRequiredService<IReservationRepository>())
                context.Reservations.Add(reservation);

                // 6. Marcar evento como procesado exitosamente
                evt.IsProcessed = true;
                _logger.LogInformation($"Reserva externa {payload.ExternalReservationId} asignada exitosamente a la habitación {availableRoom.Number}.");
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