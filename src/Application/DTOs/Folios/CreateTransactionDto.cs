using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using PmsZafiro.Domain.Enums;

namespace PmsZafiro.Application.DTOs.Folios;

public class CreateTransactionDto
{
    [Required]
    [JsonPropertyName("amount")] // Fuerza mapeo minúscula
    public decimal Amount { get; set; }
    
    [Required]
    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;
    
    [Required]
    [JsonPropertyName("type")] // ✅ ESTO CORREGIRÁ EL ERROR (Evita que sea 0 por defecto)
    public TransactionType Type { get; set; } 
    
    [JsonPropertyName("quantity")]
    public int Quantity { get; set; } = 1;

    [JsonPropertyName("unitPrice")]
    public decimal UnitPrice { get; set; }
    
    [JsonPropertyName("paymentMethod")]
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.None;

    [JsonPropertyName("cashierShiftId")]
    public Guid? CashierShiftId { get; set; }
    
    [JsonPropertyName("category")]
    public string? Category { get; set; } 
    
    [JsonPropertyName("productId")]
    public Guid? ProductId { get; set; }
}