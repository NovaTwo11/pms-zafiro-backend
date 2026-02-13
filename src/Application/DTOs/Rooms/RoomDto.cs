using PmsZafiro.Domain.Enums;

namespace PmsZafiro.Application.DTOs.Rooms;

public class RoomPriceOverrideDto
{
    public Guid RoomId { get; set; }
    public DateTime Date { get; set; }
    public decimal Price { get; set; }
}

public class RoomDto
{
    public Guid Id { get; set; }
    public string Number { get; set; } = string.Empty;
    public int Floor { get; set; }
    public string Category { get; set; } = string.Empty;
    public decimal BasePrice { get; set; }
    public string Status { get; set; } = string.Empty;
    
    // Nueva propiedad para las variaciones de precio
    public List<RoomPriceOverrideDto> PriceOverrides { get; set; } = new();
}

public class SetRoomRateDto
{
    public Guid? RoomId { get; set; }
    public string? Category { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public decimal Price { get; set; }
}