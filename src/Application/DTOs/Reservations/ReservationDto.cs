namespace PmsZafiro.Application.DTOs.Reservations;

public class ReservationDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    
    public Guid MainGuestId { get; set; }
    public string MainGuestName { get; set; } = string.Empty;
    
    // Datos globales
    public DateTime CheckIn { get; set; }
    public DateTime CheckOut { get; set; }
    public int Nights { get; set; }
    public decimal TotalAmount { get; set; }
    
    // ✅ Nueva Lista de Segmentos
    public List<ReservationSegmentDto> Segments { get; set; } = new();
}

public class ReservationSegmentDto
{
    public Guid RoomId { get; set; }
    public string RoomNumber { get; set; } = string.Empty;
    public DateTime Start { get; set; }
    public DateTime End { get; set; }
}