using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PmsZafiro.Application.DTOs.Reservations;
using PmsZafiro.Application.Interfaces;
using PmsZafiro.Domain.Entities;
using PmsZafiro.Domain.Enums;
using PmsZafiro.Infrastructure.Persistence;

namespace PmsZafiro.Infrastructure.Repositories;

public class ReservationRepository : IReservationRepository
{
    private readonly PmsDbContext _context;

    public ReservationRepository(PmsDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<Reservation>> GetAllAsync()
    {
        return await _context.Reservations
            .Include(r => r.Guest)
            .Include(r => r.Segments).ThenInclude(s => s.Room)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<ReservationDto>> GetReservationsWithLiveBalanceAsync()
    {
        var query = from r in _context.Reservations.AsNoTracking()
                    join g in _context.Guests on r.GuestId equals g.Id into guestGroup
                    from guest in guestGroup.DefaultIfEmpty()
                    join f in _context.Folios.OfType<GuestFolio>() on r.Id equals f.ReservationId into folioGroup
                    from folio in folioGroup.DefaultIfEmpty()
                    orderby r.CreatedAt descending
                    select new ReservationDto
                    {
                        Id = r.Id,
                        Code = r.ConfirmationCode,
                        Status = r.Status.ToString(),
                        MainGuestId = r.GuestId ?? Guid.Empty,
                        MainGuestName = guest != null ? (guest.FirstName + " " + guest.LastName) : "Bloqueo/Mantenimiento",
                        CheckIn = r.CheckIn,
                        CheckOut = r.CheckOut,
                        Nights = (r.CheckOut.Date - r.CheckIn.Date).Days == 0 ? 1 : (r.CheckOut.Date - r.CheckIn.Date).Days,
                        TotalAmount = r.TotalAmount,
                        Balance = folio != null ? folio.Transactions.Sum(t => 
                            (t.Type == TransactionType.Charge || t.Type == TransactionType.Expense) ? t.Amount : 
                            (t.Type == TransactionType.Payment || t.Type == TransactionType.Income) ? -t.Amount : 0) : 0,
                        PaidAmount = folio != null ? folio.Transactions
                            .Where(t => t.Type == TransactionType.Payment || t.Type == TransactionType.Income)
                            .Sum(t => t.Amount) : 0,
                        Segments = r.Segments.Select(s => new ReservationSegmentDto 
                        {
                            RoomId = s.RoomId,
                            RoomNumber = s.Room.Number,
                            Start = s.CheckIn,
                            End = s.CheckOut
                        }).ToList()
                    };

        return await query.ToListAsync();
    }

    public async Task<Reservation?> GetByIdAsync(Guid id)
    {
        return await _context.Reservations
            .Include(r => r.Guest)
            .Include(r => r.Segments).ThenInclude(s => s.Room)
            .FirstOrDefaultAsync(r => r.Id == id);
    }

    // --- MÉTODOS DE ESCRITURA (CON OUTBOX PATTERN) ---

    public async Task<Reservation> CreateAsync(Reservation reservation)
    {
        await _context.Reservations.AddAsync(reservation);
        
        // OUTBOX: Generar evento de cambio de inventario
        await GenerateAvailabilityEventsAsync(reservation);

        await _context.SaveChangesAsync();
        return reservation;
    }

    public async Task UpdateAsync(Reservation reservation)
    {
        _context.Reservations.Update(reservation);
        
        // OUTBOX: Al actualizar (cancelar, cambiar fechas), el inventario cambia
        await GenerateAvailabilityEventsAsync(reservation);

        await _context.SaveChangesAsync();
    }

    public async Task<Reservation?> GetByCodeAsync(string code)
    {
        return await _context.Reservations
            .Include(r => r.Guest)
            .Include(r => r.Segments).ThenInclude(s => s.Room)
            .FirstOrDefaultAsync(r => r.ConfirmationCode == code);
    }

    public async Task ProcessCheckOutAsync(Reservation reservation, Room? room, Folio folio)
    {
        var strategy = _context.Database.CreateExecutionStrategy();

        await strategy.ExecuteAsync(async () =>
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                _context.Reservations.Update(reservation);
                _context.Folios.Update(folio);

                if (room != null)
                {
                    _context.Rooms.Update(room);
                }

                // OUTBOX: Check-out anticipado libera inventario
                await GenerateAvailabilityEventsAsync(reservation);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        });
    }

    public async Task<IEnumerable<Reservation>> GetActiveReservationsByRoomAsync(Guid roomId)
    {
        return await _context.Reservations
            .Include(r => r.Segments)
            .Where(r => r.Segments.Any(s => s.RoomId == roomId) &&
                       (r.Status == ReservationStatus.Confirmed || r.Status == ReservationStatus.CheckedIn))
            .ToListAsync();
    }
    
    public async Task ProcessCheckInAsync(Reservation reservation, Room room, GuestFolio newFolio)
    {
        var strategy = _context.Database.CreateExecutionStrategy();

        await strategy.ExecuteAsync(async () =>
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                _context.Reservations.Update(reservation);
                _context.Rooms.Update(room);
                await _context.Folios.AddAsync(newFolio);

                // OUTBOX: Por seguridad, notificamos cambios en check-in
                await GenerateAvailabilityEventsAsync(reservation);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        });
    }

    // --- LÓGICA PRIVADA DEL OUTBOX ---
    private async Task GenerateAvailabilityEventsAsync(Reservation reservation)
    {
        // Validación defensiva: Si no hay segmentos cargados, no podemos calcular impacto
        if (reservation.Segments == null || !reservation.Segments.Any()) return;

        // Obtenemos los IDs de las habitaciones
        var roomIds = reservation.Segments.Select(s => s.RoomId).Distinct().ToList();
        
        // Cargamos información de las habitaciones para obtener la CATEGORÍA
        // Usamos ChangeTracker o DB directa para asegurar que tenemos el dato
        var roomsInfo = await _context.Rooms
            .Where(r => roomIds.Contains(r.Id))
            .ToDictionaryAsync(r => r.Id, r => r.Category);

        // Agrupamos por Categoría (String)
        var segmentsByCategory = reservation.Segments
            .GroupBy(s => roomsInfo.ContainsKey(s.RoomId) ? roomsInfo[s.RoomId] : "Unknown");

        foreach (var group in segmentsByCategory)
        {
            var category = group.Key;
            if (category == "Unknown") continue;

            // Calculamos el rango de fechas total afectado
            var minDate = group.Min(s => s.CheckIn);
            var maxDate = group.Max(s => s.CheckOut);

            var payloadObj = new
            {
                InternalCategory = category, // Ej: "Doble"
                StartDate = minDate,
                EndDate = maxDate,
                ReservationId = reservation.Id,
                Trigger = "ReservationChange"
            };

            var outboundEvent = new IntegrationOutboundEvent
            {
                Id = Guid.NewGuid(),
                EventType = IntegrationEventType.AvailabilityUpdate,
                Payload = JsonSerializer.Serialize(payloadObj),
                Status = IntegrationStatus.Pending,
                CreatedAt = DateTimeOffset.UtcNow,
                RetryCount = 0
            };

            await _context.Set<IntegrationOutboundEvent>().AddAsync(outboundEvent);
        }
    }
}