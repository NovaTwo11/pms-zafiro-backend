using System.ComponentModel.DataAnnotations;
using PmsZafiro.Domain.Enums;

namespace PmsZafiro.Application.DTOs.Folios;

public class CreateTransactionDto
{
    [Required] public decimal Amount { get; set; }
    [Required] public string Description { get; set; } = string.Empty;
    [Required] public TransactionType Type { get; set; } // 0=Charge, 1=Payment
    public int Quantity { get; set; } = 1;
    public decimal UnitPrice { get; set; }
}
