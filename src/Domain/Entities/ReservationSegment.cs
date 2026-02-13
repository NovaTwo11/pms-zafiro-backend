namespace PmsZafiro.Domain.Entities;

public class ReservationSegment
{
    public Guid Id { get; set; }
    
    public Guid ReservationId { get; set; }
    public Reservation Reservation { get; set; } = null!;

    public Guid RoomId { get; set; }
    public Room Room { get; set; } = null!;

    public DateTime CheckIn { get; set; }
    public DateTime CheckOut { get; set; }
    
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}