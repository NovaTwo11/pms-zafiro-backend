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

        // Filtrar transacciones
        var paymentsAndIncomes = shift.Transactions
            .Where(t => t.Type == TransactionType.Payment || t.Type == TransactionType.Income)
            .ToList();
            
        var expenses = shift.Transactions
            .Where(t => t.Type == TransactionType.Expense)
            .ToList();
            
        var charges = shift.Transactions
            .Where(t => t.Type == TransactionType.Charge)
            .ToList();

        // Totales
        var totalIncome = paymentsAndIncomes.Sum(t => t.Amount);
        var totalExpenses = expenses.Sum(t => t.Amount);
        
        // Efectivo Neto = (Pagos en Efectivo + Ingresos Manuales) - Gastos
        // Nota: Asumimos que los gastos siempre salen del efectivo.
        var cashIn = paymentsAndIncomes.Where(t => t.PaymentMethod == PaymentMethod.Cash).Sum(t => t.Amount);
        var netCash = cashIn - totalExpenses;

        return new CashierReportDto(
            TotalIncome: totalIncome,
            TotalCash: netCash,
            TotalCards: paymentsAndIncomes.Where(t => t.PaymentMethod == PaymentMethod.CreditCard || t.PaymentMethod == PaymentMethod.DebitCard).Sum(t => t.Amount),
            TotalTransfers: paymentsAndIncomes.Where(t => t.PaymentMethod == PaymentMethod.Transfer).Sum(t => t.Amount),
            TotalRoomCharges: charges.Sum(t => t.Amount),
            TotalExpenses: totalExpenses,
            TotalTransactions: shift.Transactions.Count
        );
    }
    
    public async Task<CashierShiftDto> CloseShiftAsync(string userId, decimal actualAmount)
    {
        var shift = await _repository.GetOpenShiftByUserIdAsync(userId);
        if (shift == null) throw new InvalidOperationException("No hay un turno abierto para cerrar.");
        
        // Calcular el esperado final usando la lógica de MapToDto (reutilización de lógica)
        // Base + (Pagos + Ingresos) - Gastos
        var calculatedDto = MapToDto(shift);
        
        shift.SystemCalculatedAmount = calculatedDto.SystemCalculatedAmount;
        shift.ActualAmount = actualAmount;
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
        var shifts = await _repository.GetHistoryAsync(); 
        return shifts.Select(MapToDto);
    }

    private static CashierShiftDto MapToDto(CashierShift s)
    {
        decimal systemAmount = s.SystemCalculatedAmount;

        // LÓGICA DE CORRECCIÓN EN TIEMPO REAL:
        if (s.Status == CashierShiftStatus.Open && s.Transactions != null)
        {
            var totalIncome = s.Transactions
                .Where(t => t.Type == TransactionType.Payment || t.Type == TransactionType.Income)
                .Sum(t => t.Amount);
            
            var totalExpenses = s.Transactions
                .Where(t => t.Type == TransactionType.Expense)
                .Sum(t => t.Amount);

            // La caja debe tener: Base + Entradas - Salidas
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