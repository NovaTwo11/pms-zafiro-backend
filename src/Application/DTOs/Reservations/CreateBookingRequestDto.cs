using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace PmsZafiro.Application.DTOs.Reservations;

public class CreateBookingRequestDto
{
    [Required]
    public Guid RoomId { get; set; }

    [Required]
    public DateOnly CheckIn { get; set; }

    [Required]
    public DateOnly CheckOut { get; set; }

    public string? DocType { get; set; }
    public string? DocNumber { get; set; }
    public string? GuestName { get; set; }
    public string? GuestEmail { get; set; }
    public string? GuestPhone { get; set; }
}