namespace PmsZafiro.Application.DTOs.Notifications;

public class NotificationDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty; // Info, Success, Warning, Error
    public bool IsRead { get; set; }
    public string TimeAgo { get; set; } = string.Empty; // Ej: "hace 5 min"
    public string? ActionUrl { get; set; }
}
