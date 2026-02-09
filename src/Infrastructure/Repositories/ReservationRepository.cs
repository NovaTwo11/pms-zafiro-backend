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
        return await _context.Reservations
            .Include(r => r.Guest)
            .Include(r => r.Room)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();
    }

    public async Task<Reservation?> GetByIdAsync(Guid id)
    {
        return await _context.Reservations
            .Include(r => r.Guest)
            .Include(r => r.Room)
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
            .Include(r => r.Room)
            .FirstOrDefaultAsync(r => r.ConfirmationCode == code);
    }

    // Este método asegura que todo se guarde a la vez.
    // Maneja el caso de room nulo para evitar crashes.
    public async Task ProcessCheckOutAsync(Reservation reservation, Room? room, Folio folio)
    {
        _context.Reservations.Update(reservation);
        _context.Folios.Update(folio);
    
        if (room != null) 
        {
            _context.Rooms.Update(room);
        }
    
        // SaveChangesAsync aplica todo en una sola transacción de DB por defecto
        await _context.SaveChangesAsync();
    }
    
    public async Task<IEnumerable<Reservation>> GetActiveReservationsByRoomAsync(Guid roomId)
    {
         return await _context.Reservations
            .Where(r => r.RoomId == roomId && 
                       (r.Status == ReservationStatus.Confirmed || r.Status == ReservationStatus.CheckedIn))
            .ToListAsync();
    }
}