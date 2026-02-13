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
        var folio = await _context.Folios
            .Include(f => f.Transactions)
            .FirstOrDefaultAsync(f => f.Id == id);

        if (folio is GuestFolio guestFolio)
        {
            await _context.Entry(guestFolio)
                .Reference(g => g.Reservation)
                .LoadAsync();

            if (guestFolio.Reservation != null)
            {
                await _context.Entry(guestFolio.Reservation).Reference(r => r.Guest).LoadAsync();
                
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
            .Include(f => f.Reservation).ThenInclude(r => r.Segments).ThenInclude(s => s.Room)
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

    // =========================================================================
    // SOLUCIÓN BUG SALDO (Caso "Sofía Gomez")
    // =========================================================================
    public async Task<decimal> GetFolioBalanceAsync(Guid folioId)
    {
        // Usamos AsNoTracking para saltarnos el caché de EF Core y leer directo de DB.
        var balanceData = await _context.Set<FolioTransaction>()
            .AsNoTracking()
            .Where(t => t.FolioId == folioId)
            .GroupBy(t => t.FolioId)
            .Select(g => new
            {
                // DEBE (Aumenta saldo/deuda):
                // 1. Charge: Consumo normal.
                // 2. Expense: Egreso de caja (ej. Devolución de dinero al cliente).
                Debits = g.Where(t => t.Type == TransactionType.Charge || 
                                      t.Type == TransactionType.Expense) 
                          .Sum(t => t.Amount),

                // HABER (Disminuye saldo/deuda):
                // 1. Payment: Pago normal del cliente.
                // 2. Income: Ingreso manual a caja vinculado al folio (Correcciones/Depósitos).
                Credits = g.Where(t => t.Type == TransactionType.Payment || 
                                       t.Type == TransactionType.Income) 
                           .Sum(t => t.Amount)
            })
            .FirstOrDefaultAsync();

        if (balanceData == null) return 0;

        return balanceData.Debits - balanceData.Credits;
    }

    public async Task<IEnumerable<GuestFolio>> GetActiveGuestFoliosAsync()
    {
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