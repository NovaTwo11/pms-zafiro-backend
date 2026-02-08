namespace PmsZafiro.Application.DTOs.Folios;

public class FolioDto
{
    public Guid Id { get; set; }
    public Guid? ReservationId { get; set; }
    public string Status { get; set; } = string.Empty;
    
    public decimal Balance { get; set; } // Calculado
    public decimal TotalCharges { get; set; }
    public decimal TotalPayments { get; set; }
    
    public List<FolioTransactionDto> Transactions { get; set; } = new();
}
