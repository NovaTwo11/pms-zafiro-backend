namespace PmsZafiro.Application.DTOs.Reservations;

public class ReservationDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    
    public Guid MainGuestId { get; set; }
    public string MainGuestName { get; set; } = string.Empty;
    
    public DateTime CheckIn { get; set; }
    public DateTime CheckOut { get; set; }
    public int Nights { get; set; }
    
    public decimal TotalAmount { get; set; }
    
    // --- NUEVOS CAMPOS PARA EL CRONOGRAMA ---
    public decimal PaidAmount { get; set; } // Lo pagado realmente en caja
    public decimal Balance { get; set; }    // La deuda real (puede ser 0 aunque TotalAmount sea > 0)
    // ----------------------------------------

    public List<ReservationSegmentDto> Segments { get; set; } = new();
}