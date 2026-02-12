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
            SystemCalculatedAmount = 0,
            ActualAmount = 0
        };

        await _repository.AddShiftAsync(shift);
        return MapToDto(shift);
    }

    // --- NUEVO MÉTODO PARA REGISTRAR GASTOS/INGRESOS ---
    public async Task<bool> RegisterMovementAsync(string userId, CreateCashierMovementDto dto)
    {
        var shift = await _repository.GetOpenShiftByUserIdAsync(userId);
        if (shift == null) throw new InvalidOperationException("No hay turno abierto para registrar movimientos.");

        // Mapeamos el string del frontend al Enum del backend
        var type = dto.Type.ToLower() == "egreso" ? TransactionType.Expense : TransactionType.Income;

        var transaction = new FolioTransaction
        {
            Id = Guid.NewGuid(),
            CashierShiftId = shift.Id,
        
            // Asignaciones corregidas:
            Amount = dto.Amount,
            Quantity = 1,
            UnitPrice = dto.Amount,
            CreatedAt = DateTimeOffset.UtcNow,       // Antes decía 'Date'
            CreatedByUserId = userId,                // Antes decía 'UserId'
        
            Description = dto.Description,
            Type = type,
            PaymentMethod = PaymentMethod.Cash,      // Efectivo por defecto para Caja Menor
        
            FolioId = null                           // Ahora permitido gracias al cambio en la entidad
        };

        // Nota: Como FolioId es null, asegúrate de no usar 'shift.Transactions.Add(transaction)'
        // si eso depende de la navegación inversa de EF Core que a veces requiere la FK.
        // Lo más seguro es agregarlo explícitamente al contexto si tienes acceso, 
        // pero si usas el repositorio y el grafo está conectado, esto debería funcionar:
        shift.Transactions.Add(transaction);
    
        await _repository.UpdateShiftAsync(shift);

        return true;
    }

    public async Task<CashierReportDto?> GetCurrentShiftReportAsync(string userId)
    {
        var shift = await _repository.GetOpenShiftByUserIdAsync(userId);
        if (shift == null) return null;

        var paymentsAndIncomes = shift.Transactions
            .Where(t => t.Type == TransactionType.Payment || t.Type == TransactionType.Income)
            .ToList();
        
        var expenses = shift.Transactions
            .Where(t => t.Type == TransactionType.Expense)
            .ToList();
        
        var charges = shift.Transactions
            .Where(t => t.Type == TransactionType.Charge)
            .ToList();

        var totalIncome = paymentsAndIncomes.Sum(t => t.Amount);
        var totalExpenses = expenses.Sum(t => t.Amount);
    
        // CORRECCIÓN SEGÚN NUEVOS ENUMS
        // Cash = 1
        var cashIn = paymentsAndIncomes.Where(t => t.PaymentMethod == PaymentMethod.Cash).Sum(t => t.Amount);
    
        // Cards = 2 y 3
        var cardsIn = paymentsAndIncomes.Where(t => t.PaymentMethod == PaymentMethod.CreditCard || t.PaymentMethod == PaymentMethod.DebitCard).Sum(t => t.Amount);
    
        // Transfers = 4
        var transfersIn = paymentsAndIncomes.Where(t => t.PaymentMethod == PaymentMethod.Transfer).Sum(t => t.Amount);

        return new CashierReportDto(
            TotalIncome: totalIncome,
            TotalCash: cashIn - totalExpenses, // Asumimos que los gastos salen del efectivo
            TotalCards: cardsIn,
            TotalTransfers: transfersIn,
            TotalRoomCharges: charges.Sum(t => t.Amount),
            TotalExpenses: totalExpenses,
            TotalTransactions: shift.Transactions.Count
        );
    }
    
    public async Task<CashierShiftDto> CloseShiftAsync(string userId, decimal actualAmount)
    {
        // 1. Aseguramos traer las transacciones desde el repositorio (ya tienes el Include en el Repo, eso está bien)
        var shift = await _repository.GetOpenShiftByUserIdAsync(userId);
        if (shift == null) throw new InvalidOperationException("No hay un turno abierto para cerrar.");
    
        // 2. CÁLCULO EXPLÍCITO (Blindaje)
        var totalIncome = shift.Transactions
            .Where(t => t.Type == TransactionType.Payment || t.Type == TransactionType.Income)
            .Sum(t => t.Amount);

        var totalExpenses = shift.Transactions
            .Where(t => t.Type == TransactionType.Expense)
            .Sum(t => t.Amount);
    
        var calculatedAmount = (shift.StartingAmount + totalIncome) - totalExpenses;
    
        // 3. Actualizamos la entidad
        shift.SystemCalculatedAmount = calculatedAmount;
        shift.ActualAmount = actualAmount;
        shift.ClosedAt = DateTimeOffset.UtcNow;
        shift.Status = CashierShiftStatus.Closed;
    
        // 4. Guardamos cambios
        await _repository.UpdateShiftAsync(shift);
    
        return MapToDto(shift);
    }
    
    public async Task<CashierShift?> GetOpenShiftEntityAsync(string userId)
    {
        return await _repository.GetOpenShiftByUserIdAsync(userId);
    }
    
    public async Task<IEnumerable<CashierShiftDto>> GetHistoryAsync()
    {
        var shifts = await _repository.GetHistoryAsync(); 
        return shifts.Select(MapToDto);
    }

    private static CashierShiftDto MapToDto(CashierShift s)
    {
        decimal systemAmount = s.SystemCalculatedAmount;

        // CORRECCIÓN: Calcular SIEMPRE si hay transacciones, sin importar si la caja está abierta o cerrada.
        // Esto arregla el historial si la base de datos tiene un valor desactualizado.
        if (s.Transactions != null && s.Transactions.Any())
        {
            var totalIncome = s.Transactions
                .Where(t => t.Type == TransactionType.Payment || t.Type == TransactionType.Income)
                .Sum(t => t.Amount);
        
            var totalExpenses = s.Transactions
                .Where(t => t.Type == TransactionType.Expense)
                .Sum(t => t.Amount);

            systemAmount = (s.StartingAmount + totalIncome) - totalExpenses;
        }

        return new CashierShiftDto(
            s.Id,
            s.UserId,
            s.OpenedAt,
            s.ClosedAt,
            s.StartingAmount,
            systemAmount, 
            s.ActualAmount,
            s.Status
        );
    }
}