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
        return await _context.Folios
            .Include(f => f.Transactions)
            .FirstOrDefaultAsync(f => f.Id == id);
    }

    public async Task<GuestFolio?> GetByReservationIdAsync(Guid reservationId)
    {
        return await _context.Folios
            .OfType<GuestFolio>()
            .Include(f => f.Transactions)
            .Include(f => f.Reservation) 
            .FirstOrDefaultAsync(f => f.ReservationId == reservationId);
    }

    public async Task CreateAsync(Folio folio)
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
            .Include(f => f.Transactions)
            .Include(f => f.Reservation)
            .ThenInclude(r => r.Guest) // ✅ CORREGIDO: MainGuest -> Guest
            .Include(f => f.Reservation)
            .ThenInclude(r => r.Room)
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