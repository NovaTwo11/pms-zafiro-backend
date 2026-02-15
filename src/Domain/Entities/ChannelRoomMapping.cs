// PmsZafiro.Domain/Entities/ChannelRoomMapping.cs
using PmsZafiro.Domain.Enums;

namespace PmsZafiro.Domain.Entities;

public class ChannelRoomMapping
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    public BookingChannel Channel { get; set; }
    
    // El string de tu categoría en PmsZafiro (Ej. "Doble Estándar")
    public string RoomCategory { get; set; } = string.Empty; 
    
    // IDs que provee Booking.com
    public string ExternalRoomId { get; set; } = string.Empty;
    public string ExternalRatePlanId { get; set; } = string.Empty;
    
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}