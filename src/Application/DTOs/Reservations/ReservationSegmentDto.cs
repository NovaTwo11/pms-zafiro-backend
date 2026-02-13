namespace PmsZafiro.Application.DTOs.Reservations;

public class ReservationSegmentDto
{
    public Guid RoomId { get; set; }
    public string RoomNumber { get; set; } = string.Empty;
    public DateTime Start { get; set; }
    public DateTime End { get; set; }
}