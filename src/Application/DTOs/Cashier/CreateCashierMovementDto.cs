namespace PmsZafiro.Application.DTOs.Cashier;

public record CreateCashierMovementDto(
    string Type, // "ingreso" o "egreso"
    decimal Amount,
    string Description
);