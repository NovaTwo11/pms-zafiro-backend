using Microsoft.EntityFrameworkCore;
using PmsZafiro.Application.Interfaces;
using PmsZafiro.Domain.Entities;
using PmsZafiro.Domain.Enums;
using PmsZafiro.Infrastructure.Persistence;

namespace PmsZafiro.Infrastructure.Repositories;

public class NotificationRepository : INotificationRepository
{
    private readonly PmsDbContext _context;

    public NotificationRepository(PmsDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<AppNotification>> GetUnreadAsync(string? userId = null)
    {
        // En el futuro filtraremos por userId
        return await _context.Notifications
            .Where(n => !n.IsRead)
            .OrderByDescending(n => n.CreatedAt)
            .ToListAsync();
    }

    public async Task AddAsync(string title, string message, NotificationType type, string? actionUrl = null)
    {
        var notif = new AppNotification
        {
            Title = title,
            Message = message,
            Type = type,
            ActionUrl = actionUrl,
            IsRead = false,
            // CreatedAt se llena solo en el DbContext
        };
        
        _context.Notifications.Add(notif);
        await _context.SaveChangesAsync();
    }

    public async Task MarkAsReadAsync(Guid id)
    {
        var notif = await _context.Notifications.FindAsync(id);
        if (notif != null)
        {
            notif.IsRead = true;
            await _context.SaveChangesAsync();
        }
    }

    public async Task MarkAllAsReadAsync()
    {
        var unread = await _context.Notifications.Where(n => !n.IsRead).ToListAsync();
        foreach (var n in unread) n.IsRead = true;
        await _context.SaveChangesAsync();
    }
}
