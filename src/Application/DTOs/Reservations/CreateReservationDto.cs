using System.ComponentModel.DataAnnotations;

namespace PmsZafiro.Application.DTOs.Reservations;

public class CreateReservationDto
{
    [Required] public Guid MainGuestId { get; set; }
    [Required] public Guid RoomId { get; set; }
    [Required] public DateOnly StartDate { get; set; }
    [Required] public DateOnly EndDate { get; set; }
}
