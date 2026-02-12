namespace PmsZafiro.Application.DTOs.Cashier;

public record CashierReportDto(
    decimal TotalIncome,        // Pagos + Ingresos manuales
    decimal TotalCash,          // (Pagos en Efectivo + Ingresos Manuales) - Gastos
    decimal TotalCards,         // Tarjetas
    decimal TotalTransfers,     // Transferencias
    decimal TotalRoomCharges,   // Cargos a habitación
    decimal TotalExpenses,      // Total de Egresos/Gastos registrados
    int TotalTransactions       // Cantidad total de movimientos
);