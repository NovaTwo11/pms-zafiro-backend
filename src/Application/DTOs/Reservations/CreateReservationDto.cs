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
    public string? GuestFirstName { get; set; }
    public string? GuestSecondName { get; set; }
    public string? GuestLastName { get; set; }
    public string? GuestSecondLastName { get; set; }
    public string? GuestDocType { get; set; }
    public string? GuestDocNumber { get; set; }
    public string? GuestEmail { get; set; }
    public string? GuestPhone { get; set; }
    public string? GuestBirthDate { get; set; }
    public string? GuestCityOrigin { get; set; }
}