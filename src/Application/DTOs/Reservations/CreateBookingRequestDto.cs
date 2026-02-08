namespace PmsZafiro.Application.DTOs.Reservations;

public class CreateBookingRequestDto
{
    public string? GuestName { get; set; }
    public string? GuestEmail { get; set; }
    public string? GuestPhone { get; set; }
    public string? DocType { get; set; }
    public string? DocNumber { get; set; }

    public Guid RoomId { get; set; }
    public DateOnly CheckIn { get; set; }
    public DateOnly CheckOut { get; set; }
    public int Adults { get; set; }
    public int Children { get; set; }
    public string? Notes { get; set; }
}
