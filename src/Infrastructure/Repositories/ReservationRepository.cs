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
            .Include(r => r.MainGuest)
            .Include(r => r.Room)
            .OrderByDescending(r => r.StartDate)
            .ToListAsync();
    }

    public async Task<Reservation?> GetByIdAsync(Guid id)
    {
        return await _context.Reservations
            .Include(r => r.MainGuest)
            .Include(r => r.Room)
            .Include(r => r.Guests)
            .FirstOrDefaultAsync(r => r.Id == id);
    }
    
    public async Task<Reservation?> GetByCodeAsync(string code)
    {
        return await _context.Reservations
            .Include(r => r.MainGuest)
            .FirstOrDefaultAsync(r => r.Code == code);
    }

    public async Task<Reservation> CreateAsync(Reservation reservation)
    {
        if (string.IsNullOrEmpty(reservation.Code))
            reservation.Code = "RES-" + new Random().Next(1000, 9999);

        _context.Reservations.Add(reservation);
        
        var detail = new ReservationGuestDetail
        {
            ReservationId = reservation.Id,
            GuestId = reservation.MainGuestId,
            IsPrimary = true,
            OriginCity = "Desconocida",
            OriginCountry = "Desconocida"
        };
        _context.ReservationGuests.Add(detail);

        var folio = new GuestFolio
        {
            Id = Guid.NewGuid(),
            ReservationId = reservation.Id,
            Status = FolioStatus.Open
        };
        _context.Folios.Add(folio);

        await _context.SaveChangesAsync();
        return reservation;
    }

    public async Task UpdateAsync(Reservation reservation)
    {
        _context.Entry(reservation).State = EntityState.Modified;
        await _context.SaveChangesAsync();
    }

    // --- LÓGICA DE CHECK-OUT ---
    public async Task ProcessCheckOutAsync(Reservation reservation, Room room, Folio folio)
    {
        // 1. Cerrar Reserva
        reservation.Status = ReservationStatus.CheckedOut;
        reservation.UpdatedAt = DateTimeOffset.UtcNow;
        _context.Entry(reservation).State = EntityState.Modified;

        // 2. Marcar Habitación como Sucia
        room.Status = RoomStatus.Dirty;
        _context.Entry(room).State = EntityState.Modified;

        // 3. Cerrar Folio
        folio.Status = FolioStatus.Closed;
        _context.Entry(folio).State = EntityState.Modified;

        // Todo se guarda en una sola transacción atómica
        await _context.SaveChangesAsync();
    }
}
