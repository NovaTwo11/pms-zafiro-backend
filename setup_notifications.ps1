$SolutionName = "PmsZafiro"
$BaseDir = Get-Location

Write-Host "Generando Sistema de Notificaciones..." -ForegroundColor Cyan

# --- CARPETAS ---
$DtoPath = "$BaseDir/src/Application/DTOs/Notifications"
$InterfacePath = "$BaseDir/src/Application/Interfaces"
$RepoPath = "$BaseDir/src/Infrastructure/Repositories"
$ControllerPath = "$BaseDir/src/API/Controllers"

New-Item -ItemType Directory -Force -Path $DtoPath | Out-Null

# --- DTOs ---
$ContentNotifDto = @"
namespace $SolutionName.Application.DTOs.Notifications;

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
"@
Set-Content -Path "$DtoPath/NotificationDto.cs" -Value $ContentNotifDto

# --- INTERFAZ ---
$ContentIRepo = @"
using $SolutionName.Domain.Entities;
using $SolutionName.Domain.Enums;

namespace $SolutionName.Application.Interfaces;

public interface INotificationRepository
{
    Task<IEnumerable<AppNotification>> GetUnreadAsync(string? userId = null);
    Task AddAsync(string title, string message, NotificationType type, string? actionUrl = null);
    Task MarkAsReadAsync(Guid id);
    Task MarkAllAsReadAsync();
}
"@
Set-Content -Path "$InterfacePath/INotificationRepository.cs" -Value $ContentIRepo

# --- REPOSITORIO ---
$ContentRepo = @"
using Microsoft.EntityFrameworkCore;
using $SolutionName.Application.Interfaces;
using $SolutionName.Domain.Entities;
using $SolutionName.Domain.Enums;
using $SolutionName.Infrastructure.Persistence;

namespace $SolutionName.Infrastructure.Repositories;

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
"@
Set-Content -Path "$RepoPath/NotificationRepository.cs" -Value $ContentRepo

# --- CONTROLADOR (API para ver las notificaciones) ---
$ContentController = @"
using Microsoft.AspNetCore.Mvc;
using $SolutionName.Application.DTOs.Notifications;
using $SolutionName.Application.Interfaces;

namespace $SolutionName.API.Controllers;

[ApiController]
[Route(""api/[controller]"")]
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

    [HttpPatch(""{id}/read"")]
    public async Task<IActionResult> MarkAsRead(Guid id)
    {
        await _repository.MarkAsReadAsync(id);
        return NoContent();
    }

    // Helper para mostrar fechas bonitas
    private string GetTimeAgo(DateTimeOffset date)
    {
        var span = DateTimeOffset.UtcNow - date;
        if (span.TotalMinutes < 1) return ""hace un momento"";
        if (span.TotalMinutes < 60) return $""hace {(int)span.TotalMinutes} min"";
        if (span.TotalHours < 24) return $""hace {(int)span.TotalHours} horas"";
        return date.ToString(""dd/MM/yyyy"");
    }
}
"@
Set-Content -Path "$ControllerPath/NotificationsController.cs" -Value $ContentController

Write-Host "¡Sistema de Notificaciones generado!" -ForegroundColor Green