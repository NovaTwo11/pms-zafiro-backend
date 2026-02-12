using System.ComponentModel.DataAnnotations;
using PmsZafiro.Domain.Enums;

namespace PmsZafiro.Application.DTOs.Folios;

public class CreateTransactionDto
{
    [Required] 
    public decimal Amount { get; set; }
    
    [Required] 
    public string Description { get; set; } = string.Empty;
    
    [Required] 
    public TransactionType Type { get; set; } 
    
    public int Quantity { get; set; } = 1;
    public decimal UnitPrice { get; set; } // Opcional si el front lo calcula, o Amount se ignora
    
    // Propiedades para POS
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Other;
    public Guid? CashierShiftId { get; set; }
    
    // Category es útil para reportes estadísticos, aunque no esté en la entidad Transaction, 
    // podrías guardarlo en Description o agregar el campo a la entidad.
    public string? Category { get; set; } 
}