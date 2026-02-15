using PmsZafiro.Domain.Enums;

namespace PmsZafiro.Domain.Entities;

public class IntegrationInboundEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    // De dónde viene (Booking.com, Expedia, etc.)
    public BookingChannel Channel { get; set; }
    
    // Aquí guardaremos el XML/JSON crudo exactamente como llega
    public string Payload { get; set; } = string.Empty;
    
    public DateTimeOffset ReceivedAt { get; set; } = DateTimeOffset.UtcNow;
    
    // Control de estado para nuestro Background Service
    public bool IsProcessed { get; set; } = false;
    
    // Si falla al procesarse, guardamos el porqué
    public string? ErrorMessage { get; set; }
}