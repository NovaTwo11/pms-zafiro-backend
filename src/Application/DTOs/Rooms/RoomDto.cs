using PmsZafiro.Domain.Enums;

namespace PmsZafiro.Application.DTOs.Rooms;

public class RoomDto
{
    public Guid Id { get; set; }
    public string Number { get; set; } = string.Empty;
    public int Floor { get; set; }
    public string Category { get; set; } = string.Empty;
    public decimal BasePrice { get; set; }
    public string Status { get; set; } = string.Empty;
}
