using PmsZafiro.Application.DTOs.Cashier;
using PmsZafiro.Application.Interfaces;
using PmsZafiro.Domain.Entities;
using PmsZafiro.Domain.Enums;

namespace PmsZafiro.Application.Services;

public class CashierService
{
    private readonly ICashierRepository _repository;

    public CashierService(ICashierRepository repository)
    {
        _repository = repository;
    }

    // Método ligero para validaciones rápidas desde otros controladores
    public async Task<bool> IsShiftOpenAsync(string userId)
    {
        var shift = await _repository.GetOpenShiftByUserIdAsync(userId);
        return shift != null;
    }

    public async Task<CashierShiftDto?> GetStatusAsync(string userId)
    {
        var shift = await _repository.GetOpenShiftByUserIdAsync(userId);
        return shift == null ? null : MapToDto(shift);
    }

    public async Task<CashierShiftDto> OpenShiftAsync(string userId, decimal startingAmount)
    {
        // Regla: No abrir si ya existe uno
        var existing = await _repository.GetOpenShiftByUserIdAsync(userId);
        if (existing != null) 
            throw new InvalidOperationException("Ya existe un turno de caja abierto para este usuario.");

        var shift = new CashierShift
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            OpenedAt = DateTimeOffset.UtcNow,
            StartingAmount = startingAmount,
            Status = CashierShiftStatus.Open,
            SystemCalculatedAmount = 0, // Se calcula al cerrar
            ActualAmount = 0
        };

        await _repository.AddShiftAsync(shift);
        return MapToDto(shift);
    }

    public async Task<CashierReportDto?> GetCurrentShiftReportAsync(string userId)
    {
        var shift = await _repository.GetOpenShiftByUserIdAsync(userId);
        if (shift == null) return null;

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
        if (shift == null) throw new InvalidOperationException("No hay un turno abierto para cerrar.");
        
        // 1. Calcular cuánto dinero debería haber según el sistema
        var totalPayments = shift.Transactions
            .Where(t => t.Type == TransactionType.Payment)
            .Sum(t => t.Amount);

        // La base inicial + Lo recaudado en pagos
        shift.SystemCalculatedAmount = shift.StartingAmount + totalPayments;
        
        // 2. Registrar lo que el humano contó
        shift.ActualAmount = actualAmount;
        
        // 3. Cerrar
        shift.ClosedAt = DateTimeOffset.UtcNow;
        shift.Status = CashierShiftStatus.Closed;
        
        await _repository.UpdateShiftAsync(shift);
        return MapToDto(shift);
    }
    
    public async Task<CashierShift?> GetOpenShiftEntityAsync(string userId)
    {
        return await _repository.GetOpenShiftByUserIdAsync(userId);
    }
    
    public async Task<IEnumerable<CashierShiftDto>> GetHistoryAsync()
    {
        var shifts = await _repository.GetHistoryAsync(); // Implementar en Repo
        return shifts.Select(MapToDto);
    }

    private static CashierShiftDto MapToDto(CashierShift s) => 
        new(s.Id, s.UserId, s.OpenedAt, s.ClosedAt, s.StartingAmount, s.SystemCalculatedAmount, s.ActualAmount, s.Status);
}