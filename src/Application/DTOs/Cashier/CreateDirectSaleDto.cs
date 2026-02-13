using PmsZafiro.Domain.Enums;

namespace PmsZafiro.Application.DTOs.Cashier;

public class CreateDirectSaleDto
{
    public decimal TotalAmount { get; set; }
    public PaymentMethod PaymentMethod { get; set; }
    public List<DirectSaleItemDto> Items { get; set; } = new();
}

public class DirectSaleItemDto
{
    public Guid ProductId { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
}