using PmsZafiro.Application.DTOs.Cashier;
using PmsZafiro.Application.Interfaces;
using PmsZafiro.Domain.Entities;
using PmsZafiro.Domain.Enums;

namespace PmsZafiro.Application.Services;

public class CashierService
{
    private readonly ICashierRepository _repository;
    private readonly IProductRepository _productRepository;

    public CashierService(ICashierRepository repository, IProductRepository productRepository)
    {
        _repository = repository;
        _productRepository = productRepository;
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
            SystemCalculatedAmount = startingAmount,
            ActualAmount = 0
        };

        await _repository.AddShiftAsync(shift);
        return MapToDto(shift);
    }

    // --- NUEVO MÉTODO: Ventas Directas (Walk-ins) ---
    public async Task RegisterDirectSaleAsync(string userId, CreateDirectSaleDto dto)
    {
        var shift = await _repository.GetOpenShiftByUserIdAsync(userId);
        if (shift == null) 
            throw new InvalidOperationException("No hay turno abierto para registrar ventas.");

        // 1. Registrar Cargos (Consumos) y Descontar Inventario
        foreach (var item in dto.Items)
        {
            var charge = new FolioTransaction
            {
                Id = Guid.NewGuid(),
                CashierShiftId = shift.Id,
                Type = TransactionType.Charge,
                Amount = item.UnitPrice * item.Quantity,
                Quantity = item.Quantity,
                UnitPrice = item.UnitPrice,
                Description = item.Description,
                CreatedAt = DateTimeOffset.UtcNow,
                CreatedByUserId = userId,
                FolioId = null, // Venta Directa (Sin huésped/Pasadía)
                PaymentMethod = PaymentMethod.None
            };
            
            await _repository.AddTransactionAsync(charge);

            // Control de Stock
            var product = await _productRepository.GetByIdAsync(item.ProductId);
            if (product != null && product.IsStockTracked)
            {
                product.Stock -= item.Quantity;
                if (product.Stock < 0) product.Stock = 0; // Prevenir negativos si no está permitido
                await _productRepository.UpdateAsync(product);
            }
        }

        // 2. Registrar el Pago (Ingreso Monetario)
        var payment = new FolioTransaction
        {
            Id = Guid.NewGuid(),
            CashierShiftId = shift.Id,
            Type = TransactionType.Payment,
            Amount = dto.TotalAmount,
            Quantity = 1,
            UnitPrice = dto.TotalAmount,
            Description = $"Pago POS - Venta Directa Público General",
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedByUserId = userId,
            FolioId = null, // Venta Directa
            PaymentMethod = dto.PaymentMethod
        };

        await _repository.AddTransactionAsync(payment);

        // 3. Actualizar la caja base del Turno
        shift.SystemCalculatedAmount += dto.TotalAmount;
        await _repository.UpdateShiftAsync(shift);
    }

    public async Task RegisterMovementAsync(string userId, CreateCashierMovementDto dto)
    {
        // 1. Obtener el turno (Viene trackeado por EF)
        var shift = await _repository.GetOpenShiftByUserIdAsync(userId);
        if (shift == null) 
            throw new InvalidOperationException("No hay turno abierto para registrar movimientos.");

        // 2. Mapear tipo
        TransactionType type;
        var typeNormalized = dto.Type?.Trim().ToLower();
        if (typeNormalized == "ingreso") type = TransactionType.Income;
        else if (typeNormalized == "egreso") type = TransactionType.Expense;
        else throw new ArgumentException($"Tipo no válido: {dto.Type}");

        // 3. Crear la transacción
        var transaction = new FolioTransaction
        {
            Id = Guid.NewGuid(),
            CashierShiftId = shift.Id,
            Amount = dto.Amount,
            Quantity = 1,
            UnitPrice = dto.Amount,
            Type = type,
            PaymentMethod = PaymentMethod.Cash,
            Description = dto.Description,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedByUserId = userId,
            FolioId = null // Importante: Movimiento de caja menor no requiere Folio
        };

        // 4. GUARDADO EXPLÍCITO: Primero guardamos la transacción (INSERT)
        await _repository.AddTransactionAsync(transaction);

        // 5. ACTUALIZACIÓN DEL SALDO: Luego actualizamos la propiedad del turno (UPDATE)
        if (type == TransactionType.Income)
            shift.SystemCalculatedAmount += dto.Amount;
        else 
            shift.SystemCalculatedAmount -= dto.Amount;

        await _repository.UpdateShiftAsync(shift);
    }

    public async Task<CashierReportDto?> GetCurrentShiftReportAsync(string userId)
    {
        var shift = await _repository.GetOpenShiftByUserIdAsync(userId);
        if (shift == null) return null;

        // Clasificación de Transacciones
        var payments = shift.Transactions.Where(t => t.Type == TransactionType.Payment).ToList(); // Pagos Reservas y Ventas Directas
        var incomes = shift.Transactions.Where(t => t.Type == TransactionType.Income).ToList();   // Ingresos Caja
        var expenses = shift.Transactions.Where(t => t.Type == TransactionType.Expense).ToList(); // Gastos Caja
        var charges = shift.Transactions.Where(t => t.Type == TransactionType.Charge).ToList();   // Cargos Habitación y POS

        // Totales
        var totalReservationsPayment = payments.Sum(t => t.Amount);
        var totalPettyCashIncome = incomes.Sum(t => t.Amount);
        var totalPettyCashExpense = expenses.Sum(t => t.Amount);

        // Desglose por Método de Pago (Pagos + Ingresos Caja)
        var allInflow = payments.Concat(incomes).ToList();
        
        var cashIn = allInflow.Where(t => t.PaymentMethod == PaymentMethod.Cash).Sum(t => t.Amount);
        var cardsIn = allInflow.Where(t => t.PaymentMethod == PaymentMethod.CreditCard || t.PaymentMethod == PaymentMethod.DebitCard).Sum(t => t.Amount);
        var transfersIn = allInflow.Where(t => t.PaymentMethod == PaymentMethod.Transfer).Sum(t => t.Amount);

        // Total Income = Todo lo positivo que entró
        var totalIncome = totalReservationsPayment + totalPettyCashIncome;

        return new CashierReportDto(
            TotalIncome: totalIncome,
            TotalCash: cashIn - totalPettyCashExpense, // Efectivo neto en caja
            TotalCards: cardsIn,
            TotalTransfers: transfersIn,
            TotalRoomCharges: charges.Sum(t => t.Amount),
            TotalExpenses: totalPettyCashExpense,
            TotalTransactions: shift.Transactions.Count
        );
    }
    
    public async Task<CashierShiftDto> CloseShiftAsync(string userId, decimal actualAmount)
    {
        var shift = await _repository.GetOpenShiftByUserIdAsync(userId);
        if (shift == null) throw new InvalidOperationException("No hay un turno abierto para cerrar.");
    
        // Recalculamos todo para asegurar integridad
        var calculated = CalculateSystemAmount(shift);
    
        shift.SystemCalculatedAmount = calculated;
        shift.ActualAmount = actualAmount;
        shift.ClosedAt = DateTimeOffset.UtcNow;
        shift.Status = CashierShiftStatus.Closed;
    
        await _repository.UpdateShiftAsync(shift);
    
        return MapToDto(shift);
    }
    
    public async Task<IEnumerable<CashierShiftDto>> GetHistoryAsync()
    {
        var shifts = await _repository.GetHistoryAsync(); 
        return shifts.OrderByDescending(s => s.OpenedAt).Select(MapToDto);
    }

    // --- MÉTODOS AUXILIARES ---

    private static decimal CalculateSystemAmount(CashierShift s)
    {
        if (s.Transactions == null || !s.Transactions.Any()) 
            return s.StartingAmount;

        // Dinero que suma
        var inflows = s.Transactions
            .Where(t => t.Type == TransactionType.Payment || t.Type == TransactionType.Income)
            .Sum(t => t.Amount);
        
        // Dinero que resta
        var outflows = s.Transactions
            .Where(t => t.Type == TransactionType.Expense)
            .Sum(t => t.Amount);

        return (s.StartingAmount + inflows) - outflows;
    }

    private static CashierShiftDto MapToDto(CashierShift s)
    {
        decimal systemAmount = s.Status == CashierShiftStatus.Closed 
            ? s.SystemCalculatedAmount 
            : CalculateSystemAmount(s);

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
    
    /// <summary>
    /// Obtiene la entidad completa del turno abierto, incluyendo sus transacciones.
    /// Útil para procesos internos que requieren el objeto de dominio completo.
    /// </summary>
    public async Task<CashierShift?> GetOpenShiftEntityAsync(string userId)
    {
        // Utilizamos el repositorio para obtener el turno con sus relaciones (Include(t => t.Transactions))
        // El repositorio ya debería manejar la lógica de filtrar por Status == Open
        var shift = await _repository.GetOpenShiftByUserIdAsync(userId);
    
        if (shift == null)
        {
            return null;
        }

        // Opcional: Recalcular el monto del sistema antes de devolver la entidad 
        // para asegurar que cualquier proceso posterior tenga el dato fresco
        shift.SystemCalculatedAmount = CalculateSystemAmount(shift);
    
        return shift;
    }
}