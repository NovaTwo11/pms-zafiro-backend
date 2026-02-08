using PmsZafiro.Domain.Enums;

namespace PmsZafiro.Domain.Entities;

public class Reservation
{
    public Guid Id { get; set; }
    public string ConfirmationCode { get; set; } = string.Empty;
    
    // Relaciones
    public Guid GuestId { get; set; }
    public Guest Guest { get; set; } = null!; // ✅ Propiedad de navegación necesaria
    
    public Guid RoomId { get; set; }
    public Room Room { get; set; } = null!;   // ✅ Propiedad de navegación necesaria

    public DateTime CheckIn { get; set; }
    public DateTime CheckOut { get; set; }
    
    public ReservationStatus Status { get; set; } = ReservationStatus.Pending;
    public int Adults { get; set; }
    public int Children { get; set; }
    public decimal TotalAmount { get; set; }
    public string? Notes { get; set; }
    
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}