using PmsZafiro.Domain.Enums;

namespace PmsZafiro.Domain.Entities;

public class IntegrationOutboundEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    // Tipo: AvailabilityUpdate
    public IntegrationEventType EventType { get; set; } 
    
    // Payload JSON: { "RoomCategory": "Doble", "StartDate": "2026-02-20", "EndDate": "2026-02-25" }
    public string Payload { get; set; } = string.Empty;
    
    public IntegrationStatus Status { get; set; } = IntegrationStatus.Pending;
    public string? ErrorMessage { get; set; }
    public int RetryCount { get; set; } = 0;
    
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ProcessedAt { get; set; }
}