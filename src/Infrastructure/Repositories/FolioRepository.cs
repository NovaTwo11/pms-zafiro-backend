using Microsoft.EntityFrameworkCore;
using PmsZafiro.Application.Interfaces;
using PmsZafiro.Domain.Entities;
using PmsZafiro.Domain.Enums;
using PmsZafiro.Infrastructure.Persistence;

namespace PmsZafiro.Infrastructure.Repositories;

public class FolioRepository : IFolioRepository
{
    private readonly PmsDbContext _context;

    public FolioRepository(PmsDbContext context)
    {
        _context = context;
    }

    public async Task<Folio?> GetByIdAsync(Guid id)
    {
        // Carga base polimórfica (trae GuestFolio o ExternalFolio según el discriminador)
        var folio = await _context.Folios
            .Include(f => f.Transactions)
            .FirstOrDefaultAsync(f => f.Id == id);

        // Carga explicita de relaciones solo si es GuestFolio
        if (folio is GuestFolio guestFolio)
        {
            await _context.Entry(guestFolio)
                .Reference(g => g.Reservation)
                .LoadAsync();

            if (guestFolio.Reservation != null)
            {
                await _context.Entry(guestFolio.Reservation).Reference(r => r.Guest).LoadAsync();
                await _context.Entry(guestFolio.Reservation).Reference(r => r.Room).LoadAsync();
            }
        }

        return folio;
    }

    public async Task<GuestFolio?> GetByReservationIdAsync(Guid reservationId)
    {
        return await _context.Folios
            .OfType<GuestFolio>() // Filtra solo GuestFolios
            .Include(f => f.Transactions)
            .Include(f => f.Reservation)
            .FirstOrDefaultAsync(f => f.ReservationId == reservationId);
    }

    public async Task<IEnumerable<Folio>> GetAllAsync()
    {
        return await _context.Folios
            .Include(f => f.Transactions)
            .ToListAsync();
    }

    public async Task AddAsync(Folio folio)
    {
        await _context.Folios.AddAsync(folio);
        await _context.SaveChangesAsync();
    }

    public async Task AddTransactionAsync(FolioTransaction transaction)
    {
        await _context.Set<FolioTransaction>().AddAsync(transaction);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(Folio folio)
    {
        _context.Folios.Update(folio);
        await _context.SaveChangesAsync();
    }

    public async Task<IEnumerable<GuestFolio>> GetActiveGuestFoliosAsync()
    {
        return await _context.Folios
            .OfType<GuestFolio>()
            .Include(f => f.Reservation).ThenInclude(r => r.Guest)
            .Include(f => f.Reservation).ThenInclude(r => r.Room)
            .Include(f => f.Transactions)
            .Where(f => f.Status == FolioStatus.Open)
            .ToListAsync();
    }

    public async Task<IEnumerable<ExternalFolio>> GetActiveExternalFoliosAsync()
    {
        return await _context.Folios
            .OfType<ExternalFolio>()
            .Include(f => f.Transactions)
            .Where(f => f.Status == FolioStatus.Open)
            .ToListAsync();
    }
}