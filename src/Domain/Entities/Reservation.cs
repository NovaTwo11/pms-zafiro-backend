using PmsZafiro.Domain.Enums;

namespace PmsZafiro.Domain.Entities;

public class Reservation
{
    public Guid Id { get; set; }
    public string ConfirmationCode { get; set; } = string.Empty;
    
    public Guid GuestId { get; set; }
    public Guest Guest { get; set; } = null!;

    // ✅ CAMBIO CLAVE: Relación 1:N con Segmentos
    public ICollection<ReservationSegment> Segments { get; set; } = new List<ReservationSegment>();

    // CheckIn/CheckOut globales (limites de la reserva completa)
    public DateTime CheckIn { get; set; }
    public DateTime CheckOut { get; set; }
    
    public ReservationStatus Status { get; set; } = ReservationStatus.Pending;
    public int Adults { get; set; }
    public int Children { get; set; }
    public decimal TotalAmount { get; set; }
    public string? Notes { get; set; }
    
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}