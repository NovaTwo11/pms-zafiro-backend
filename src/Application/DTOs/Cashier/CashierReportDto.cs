namespace PmsZafiro.Application.DTOs.Cashier;

public record CashierReportDto(
    decimal TotalIncome,        // Total Dinero Recaudado (Pagos)
    decimal TotalCash,          // Efectivo en Cajón
    decimal TotalCards,         // Tarjetas (Débito/Crédito)
    decimal TotalTransfers,     // Transferencias
    decimal TotalRoomCharges,   // Ventas fiadas a la habitación (No entra dinero ahora)
    int TotalTransactions       // Cantidad de movimientos
);