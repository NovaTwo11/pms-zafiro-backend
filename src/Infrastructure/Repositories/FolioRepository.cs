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
        // Carga base polimórfica
        var folio = await _context.Folios
            .Include(f => f.Transactions)
            .FirstOrDefaultAsync(f => f.Id == id);

        // Carga explícita de relaciones complejas para GuestFolio
        if (folio is GuestFolio guestFolio)
        {
            await _context.Entry(guestFolio)
                .Reference(g => g.Reservation)
                .LoadAsync();

            if (guestFolio.Reservation != null)
            {
                await _context.Entry(guestFolio.Reservation).Reference(r => r.Guest).LoadAsync();
                
                // REFACTORIZADO: Cargar Segmentos -> Habitación en lugar de Habitación directa
                await _context.Entry(guestFolio.Reservation)
                    .Collection(r => r.Segments)
                    .Query()
                    .Include(s => s.Room)
                    .LoadAsync();
            }
        }

        return folio;
    }

    public async Task<GuestFolio?> GetByReservationIdAsync(Guid reservationId)
    {
        return await _context.Folios
            .OfType<GuestFolio>()
            .Include(f => f.Transactions)
            .Include(f => f.Reservation).ThenInclude(r => r.Segments).ThenInclude(s => s.Room) // Incluimos segmentos
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
        // REFACTORIZADO: Incluir Segmentos y sus Habitaciones
        return await _context.Folios
            .OfType<GuestFolio>()
            .Include(f => f.Reservation).ThenInclude(r => r.Guest)
            .Include(f => f.Reservation).ThenInclude(r => r.Segments).ThenInclude(s => s.Room)
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