using PmsZafiro.Domain.Enums;

namespace PmsZafiro.Domain.Entities;

public class Room
{
    public Guid Id { get; set; }
    public string Number { get; set; } = string.Empty;
    public int Floor { get; set; }
    public string Category { get; set; } = String.Empty;
    public decimal BasePrice { get; set; }
    public RoomStatus Status { get; set; }
    
    // Relación: Precios personalizados por fecha
    public ICollection<RoomPriceOverride> PriceOverrides { get; set; } = new List<RoomPriceOverride>();
}

public class RoomPriceOverride
{
    public Guid Id { get; set; }
    public Guid RoomId { get; set; }
    public DateOnly Date { get; set; }
    public decimal Price { get; set; }
}
