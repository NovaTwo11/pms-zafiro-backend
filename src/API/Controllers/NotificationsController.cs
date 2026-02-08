using Microsoft.AspNetCore.Mvc;
using PmsZafiro.Application.DTOs.Notifications;
using PmsZafiro.Application.Interfaces;

namespace PmsZafiro.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class NotificationsController : ControllerBase
{
    private readonly INotificationRepository _repository;

    public NotificationsController(INotificationRepository repository)
    {
        _repository = repository;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<NotificationDto>>> GetUnread()
    {
        var notifs = await _repository.GetUnreadAsync();
        
        var dtos = notifs.Select(n => new NotificationDto
        {
            Id = n.Id,
            Title = n.Title,
            Message = n.Message,
            Type = n.Type.ToString(),
            IsRead = n.IsRead,
            ActionUrl = n.ActionUrl,
            TimeAgo = GetTimeAgo(n.CreatedAt)
        });

        return Ok(dtos);
    }

    [HttpPatch("{id}/read")]
    public async Task<IActionResult> MarkAsRead(Guid id)
    {
        await _repository.MarkAsReadAsync(id);
        return NoContent();
    }

    // Helper para mostrar fechas bonitas
    private string GetTimeAgo(DateTimeOffset date)
    {
        var span = DateTimeOffset.UtcNow - date;
        if (span.TotalMinutes < 1) return "hace un momento";
        if (span.TotalMinutes < 60) return $"hace {(int)span.TotalMinutes} min";
        if (span.TotalHours < 24) return $"hace {(int)span.TotalHours} horas";
        return date.ToString("dd/MM/yyyy");
    }
}
