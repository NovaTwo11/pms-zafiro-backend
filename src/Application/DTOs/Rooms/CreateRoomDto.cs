using System.ComponentModel.DataAnnotations;
using PmsZafiro.Domain.Enums;

namespace PmsZafiro.Application.DTOs.Rooms;

public class CreateRoomDto
{
    [Required] public string Number { get; set; } = string.Empty;
    public int Floor { get; set; }
    [Required] public string Category { get; set; } = String.Empty;
    [Required] public decimal BasePrice { get; set; }
    public RoomStatus Status { get; set; } = RoomStatus.Available;
}
