namespace PmsZafiro.Application.DTOs.Folios;

public class FolioTransactionDto
{
    public Guid Id { get; set; }
    public string Date { get; set; } = string.Empty; // Formateada
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Type { get; set; } = string.Empty; // Charge, Payment
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
    public string User { get; set; } = string.Empty;
}
