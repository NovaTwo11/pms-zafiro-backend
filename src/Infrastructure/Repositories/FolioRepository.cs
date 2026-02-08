using Microsoft.EntityFrameworkCore;
using PmsZafiro.Application.Interfaces;
using PmsZafiro.Domain.Entities;
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
            .Include(f => f.Transactions.OrderByDescending(t => t.CreatedAt))
            .FirstOrDefaultAsync(f => f.Id == id);
    }

    public async Task<Folio?> GetByReservationIdAsync(Guid reservationId)
    {
        // Buscamos el GuestFolio asociado a esa reserva
        return await _context.Folios
            .OfType<GuestFolio>() // Filtramos solo folios de huésped
            .Include(f => f.Transactions.OrderByDescending(t => t.CreatedAt))
            .FirstOrDefaultAsync(f => f.ReservationId == reservationId);
    }

    public async Task AddTransactionAsync(FolioTransaction transaction)
    {
        _context.Transactions.Add(transaction);
        await _context.SaveChangesAsync();
    }
}
