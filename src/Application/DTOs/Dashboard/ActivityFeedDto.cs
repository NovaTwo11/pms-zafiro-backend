namespace PmsZafiro.Application.DTOs.Dashboard;

public class ActivityFeedDto
{
    public string Id { get; set; } = string.Empty;
    
    /// <summary>
    /// Nombre del usuario o huésped.
    /// </summary>
    public string User { get; set; } = string.Empty;
    
    /// <summary>
    /// Descripción de la acción (ej. "Nueva Reserva", "Check-in").
    /// </summary>
    public string Action { get; set; } = string.Empty;
    
    /// <summary>
    /// Hora del evento (HH:mm).
    /// </summary>
    public string Time { get; set; } = string.Empty;
    
    /// <summary>
    /// Monto asociado formateado (si aplica).
    /// </summary>
    public string Amount { get; set; } = string.Empty;
    
    /// <summary>
    /// URL del avatar (opcional).
    /// </summary>
    public string Avatar { get; set; } = string.Empty;
    
    /// <summary>
    /// Iniciales para mostrar si no hay avatar.
    /// </summary>
    public string Initials { get; set; } = string.Empty;
}