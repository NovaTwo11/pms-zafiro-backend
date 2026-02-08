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

    // âœ… IMPLEMENTACIÃ“N FALTANTE 1: CreateAsync
    public async Task<Reservation> CreateAsync(Reservation reservation)
    {
        await _context.Reservations.AddAsync(reservation);
        await _context.SaveChangesAsync();
        return reservation;
    }

    // âœ… IMPLEMENTACIÃ“N FALTANTE 2: UpdateAsync
    public async Task UpdateAsync(Reservation reservation)
    {
        _context.Reservations.Update(reservation);
        await _context.SaveChangesAsync();
    }

    // âœ… IMPLEMENTACIÃ“N FALTANTE 3: GetByCodeAsync
    public async Task<Reservation?> GetByCodeAsync(string code)
    {
        return await _context.Reservations
            .Include(r => r.Guest)
            .Include(r => r.Room)
            .FirstOrDefaultAsync(r => r.ConfirmationCode == code);
    }

    // âœ… IMPLEMENTACIÃ“N FALTANTE 4: ProcessCheckOutAsync (Transaccional)
    public async Task ProcessCheckOutAsync(Reservation reservation, Room room, Folio folio)
    {
        // EF Core maneja esto como una transacciÃ³n implÃ­cita al llamar SaveChanges una sola vez
        _context.Reservations.Update(reservation);
        _context.Rooms.Update(room);
        _context.Folios.Update(folio);
        
        await _context.SaveChangesAsync();
    }
}