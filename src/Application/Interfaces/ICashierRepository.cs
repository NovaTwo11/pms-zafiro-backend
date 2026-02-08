using PmsZafiro.Domain.Entities;
namespace PmsZafiro.Application.Interfaces;
public interface ICashierRepository
{
    Task<CashierShift?> GetOpenShiftByUserIdAsync(string userId);
    Task AddShiftAsync(CashierShift shift);
    Task UpdateShiftAsync(CashierShift shift);
    Task<CashierShift?> GetShiftByIdAsync(Guid id);
}