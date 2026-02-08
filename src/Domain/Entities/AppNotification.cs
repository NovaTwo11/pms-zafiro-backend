using PmsZafiro.Domain.Enums;

namespace PmsZafiro.Domain.Entities;

public class AppNotification
{
    public Guid Id { get; set; }
    
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public NotificationType Type { get; set; } // Info, Warning, Error
    
    public string? TargetRole { get; set; } // Ej: 'Admin', 'Reception'
    public string? TargetUserId { get; set; } // Si es para alguien específico
    
    public bool IsRead { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    
    // Navegación: Al hacer click, ¿a dónde va?
    // Ej: '/reservas/123-abc' o '/folios/555'
    public string? ActionUrl { get; set; } 
    
    // Opcional: Relacionar con entidad para integridad
    public Guid? RelatedEntityId { get; set; }
    public string? RelatedEntityType { get; set; } // 'Reservation', 'Folio'
}
