using PmsZafiro.Application.DTOs.Cashier;
using PmsZafiro.Application.Interfaces;
using PmsZafiro.Domain.Entities;
using PmsZafiro.Domain.Enums;

namespace PmsZafiro.Application.Services;

public class CashierService
{
    private readonly ICashierRepository _repository;
    public CashierService(ICashierRepository repository) { _repository = repository; }

    public async Task<CashierShiftDto?> GetStatusAsync(string userId)
    {
        var shift = await _repository.GetOpenShiftByUserIdAsync(userId);
        return shift == null ? null : MapToDto(shift);
    }

    public async Task<CashierShiftDto> OpenShiftAsync(string userId, decimal startingAmount)
    {
        var existing = await _repository.GetOpenShiftByUserIdAsync(userId);
        if (existing != null) throw new InvalidOperationException("Shift already open.");
        var shift = new CashierShift { Id = Guid.NewGuid(), UserId = userId, OpenedAt = DateTimeOffset.UtcNow, StartingAmount = startingAmount, Status = CashierShiftStatus.Open };
        await _repository.AddShiftAsync(shift);
        return MapToDto(shift);
    }

    public async Task<CashierReportDto?> GetCurrentShiftReportAsync(string userId)
    {
        // Obtenemos el turno abierto con sus transacciones (gracias al Include del repositorio)
        var shift = await _repository.GetOpenShiftByUserIdAsync(userId);
        if (shift == null) return null;

        // Filtramos transacciones del turno actual
        var payments = shift.Transactions.Where(t => t.Type == TransactionType.Payment).ToList();
        var charges = shift.Transactions.Where(t => t.Type == TransactionType.Charge).ToList();

        return new CashierReportDto(
            TotalIncome: payments.Sum(t => t.Amount),
            TotalCash: payments.Where(t => t.PaymentMethod == PaymentMethod.Cash).Sum(t => t.Amount),
            TotalCards: payments.Where(t => t.PaymentMethod == PaymentMethod.CreditCard || t.PaymentMethod == PaymentMethod.DebitCard).Sum(t => t.Amount),
            TotalTransfers: payments.Where(t => t.PaymentMethod == PaymentMethod.Transfer).Sum(t => t.Amount),
            TotalRoomCharges: charges.Sum(t => t.Amount),
            TotalTransactions: shift.Transactions.Count
        );
    }
    
    public async Task<CashierShiftDto> CloseShiftAsync(string userId, decimal actualAmount)
    {
        var shift = await _repository.GetOpenShiftByUserIdAsync(userId);
        if (shift == null) throw new InvalidOperationException("No open shift found.");
        
        var totalPayments = shift.Transactions.Where(t => t.Type == TransactionType.Payment).Sum(t => t.Amount);
        shift.SystemCalculatedAmount = shift.StartingAmount + totalPayments;
        shift.ActualAmount = actualAmount;
        shift.ClosedAt = DateTimeOffset.UtcNow;
        shift.Status = CashierShiftStatus.Closed;
        
        await _repository.UpdateShiftAsync(shift);
        return MapToDto(shift);
    }

    private static CashierShiftDto MapToDto(CashierShift s) => new(s.Id, s.UserId, s.OpenedAt, s.ClosedAt, s.StartingAmount, s.SystemCalculatedAmount, s.ActualAmount, s.Status);
}