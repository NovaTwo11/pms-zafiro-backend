namespace PmsZafiro.Application.DTOs.Folios;

public class FolioDto
{
    public Guid Id { get; set; }
    public Guid? ReservationId { get; set; }
    public string Status { get; set; } = string.Empty;
    
    public decimal Balance { get; set; }
    public decimal TotalCharges { get; set; }
    public decimal TotalPayments { get; set; }
    
    // --- NUEVOS CAMPOS NECESARIOS ---
    public string? GuestName { get; set; }
    public string? RoomNumber { get; set; }
    public string? Alias { get; set; }
    public string? Description { get; set; }
    // --------------------------------

    public List<FolioTransactionDto> Transactions { get; set; } = new();
}