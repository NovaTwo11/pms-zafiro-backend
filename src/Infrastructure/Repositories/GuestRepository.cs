using Microsoft.EntityFrameworkCore;
using PmsZafiro.Application.Interfaces;
using PmsZafiro.Domain.Entities;
using PmsZafiro.Infrastructure.Persistence;

namespace PmsZafiro.Infrastructure.Repositories;

public class GuestRepository : IGuestRepository
{
    private readonly PmsDbContext _context;

    public GuestRepository(PmsDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<Guest>> GetAllAsync()
    {
        return await _context.Guests.ToListAsync();
    }

    public async Task<IEnumerable<Guest>> GetAllWithHistoryAsync()
    {
        return await _context.Guests
            .Include(g => g.Reservations)
            .OrderByDescending(g => g.CreatedAt)
            .ToListAsync();
    }

    public async Task<Guest?> GetByIdAsync(Guid id)
    {
        return await _context.Guests.FindAsync(id);
        
    }

    public async Task<Guest?> GetByDocumentAsync(string documentNumber)
    {
        return await _context.Guests
            .FirstOrDefaultAsync(g => g.DocumentNumber == documentNumber);
    }

    public async Task AddAsync(Guest guest)
    {
        await _context.Guests.AddAsync(guest);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(Guest guest)
    {
        _context.Entry(guest).State = EntityState.Modified;
        await _context.SaveChangesAsync();        
    }

    public async Task DeleteAsync(Guid id)
    {
        var guest = await _context.Guests.FindAsync(id);
        if (guest != null)
        {
            _context.Guests.Remove(guest);
            await _context.SaveChangesAsync();
        }
    }
}
