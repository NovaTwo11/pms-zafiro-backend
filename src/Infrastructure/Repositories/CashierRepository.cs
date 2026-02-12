using Microsoft.EntityFrameworkCore;
using PmsZafiro.Application.Interfaces;
using PmsZafiro.Domain.Entities;
using PmsZafiro.Domain.Enums;
using PmsZafiro.Infrastructure.Persistence;

namespace PmsZafiro.Infrastructure.Repositories;

public class CashierRepository : ICashierRepository
{
    private readonly PmsDbContext _context;

    public CashierRepository(PmsDbContext context)
    {
        _context = context;
    }

    public async Task<CashierShift?> GetOpenShiftByUserIdAsync(string userId)
    {
        // El Include es vital para sumar el dinero al cerrar caja
        return await _context.CashierShifts
            .Include(s => s.Transactions) 
            .FirstOrDefaultAsync(s => s.UserId == userId && s.Status == CashierShiftStatus.Open);
    }

    public async Task AddShiftAsync(CashierShift shift)
    {
        _context.CashierShifts.Add(shift);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateShiftAsync(CashierShift shift)
    {
        _context.CashierShifts.Update(shift);
        await _context.SaveChangesAsync();
    }

    public async Task<CashierShift?> GetShiftByIdAsync(Guid id)
    {
        return await _context.CashierShifts.FindAsync(id);
    }
    
    public async Task<List<CashierShift>> GetHistoryAsync()
    {
        return await _context.CashierShifts
            .AsNoTracking()
            .Include(s => s.Transactions)
            .OrderByDescending(s => s.OpenedAt)
            .Take(30) // Últimos 30 turnos
            .ToListAsync();
    }
}