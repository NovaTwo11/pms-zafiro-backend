using Microsoft.EntityFrameworkCore;
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
        // REFACTORIZADO: Incluir Segmentos -> Room
        return await _context.Reservations
            .Include(r => r.Guest)
            .Include(r => r.Segments).ThenInclude(s => s.Room)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();
    }

    public async Task<Reservation?> GetByIdAsync(Guid id)
    {
        return await _context.Reservations
            .Include(r => r.Guest)
            .Include(r => r.Segments).ThenInclude(s => s.Room)
            .FirstOrDefaultAsync(r => r.Id == id);
    }

    public async Task<Reservation> CreateAsync(Reservation reservation)
    {
        await _context.Reservations.AddAsync(reservation);
        await _context.SaveChangesAsync();
        return reservation;
    }

    public async Task UpdateAsync(Reservation reservation)
    {
        _context.Reservations.Update(reservation);
        await _context.SaveChangesAsync();
    }

    public async Task<Reservation?> GetByCodeAsync(string code)
    {
        return await _context.Reservations
            .Include(r => r.Guest)
            .Include(r => r.Segments).ThenInclude(s => s.Room)
            .FirstOrDefaultAsync(r => r.ConfirmationCode == code);
    }

    // --- LÓGICA DE CHECK-OUT TRANSACCIONAL ---
    public async Task ProcessCheckOutAsync(Reservation reservation, Room? room, Folio folio)
    {
        var strategy = _context.Database.CreateExecutionStrategy();

        await strategy.ExecuteAsync(async () =>
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // 1. Actualizar Reserva
                _context.Reservations.Update(reservation);
                
                // 2. Actualizar Folio
                _context.Folios.Update(folio);

                // 3. Actualizar Habitación (si existe, pasada por el Controller)
                if (room != null)
                {
                    _context.Rooms.Update(room);
                }

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
        // REFACTORIZADO: Buscar si ALGÚN segmento de la reserva usa esa habitación
        // y la reserva está activa.
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
                // 1. Actualizar Reserva
                _context.Reservations.Update(reservation);
            
                // 2. Actualizar Habitación (Pasada por el controller, derivada del primer segmento)
                _context.Rooms.Update(room);

                // 3. Crear Folio
                await _context.Folios.AddAsync(newFolio);

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
}