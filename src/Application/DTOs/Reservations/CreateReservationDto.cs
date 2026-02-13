using System;
using System.ComponentModel.DataAnnotations;

namespace PmsZafiro.Application.DTOs.Reservations;

public class CreateReservationDto
{
    [Required] public Guid RoomId { get; set; }
    public string? MainGuestId { get; set; }
    [Required] public string MainGuestName { get; set; } = null!;
    [Required] public DateTime CheckIn { get; set; }
    [Required] public DateTime CheckOut { get; set; }
    public int Adults { get; set; }
    public int Children { get; set; }
    public string? Status { get; set; }
    public string? SpecialRequests { get; set; }
}