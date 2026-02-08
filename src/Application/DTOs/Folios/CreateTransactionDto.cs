using System.ComponentModel.DataAnnotations;
using PmsZafiro.Domain.Enums;

namespace PmsZafiro.Application.DTOs.Folios;

public class CreateTransactionDto
{
    [Required] public decimal Amount { get; set; }
    [Required] public string Description { get; set; } = string.Empty;
    [Required] public TransactionType Type { get; set; } 
    public int Quantity { get; set; } = 1;
    public decimal UnitPrice { get; set; }
    
    // Propiedades para POS
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.None;
    public Guid? CashierShiftId { get; set; }
    public string? Category { get; set; } // Se recibe del front pero no se guarda si la entidad no lo tiene
}
