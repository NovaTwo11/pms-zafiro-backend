using PmsZafiro.Domain.Entities;
using PmsZafiro.Domain.Enums;

namespace PmsZafiro.Application.Interfaces;

public interface INotificationRepository
{
    Task<IEnumerable<AppNotification>> GetUnreadAsync(string? userId = null);
    Task AddAsync(string title, string message, NotificationType type, string? actionUrl = null);
    Task MarkAsReadAsync(Guid id);
    Task MarkAllAsReadAsync();
}
