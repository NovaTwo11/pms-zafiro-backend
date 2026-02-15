using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PmsZafiro.Application.DTOs.Dashboard;
using PmsZafiro.Domain.Entities; // Asegúrate de tener este using
using PmsZafiro.Domain.Enums;
using PmsZafiro.Infrastructure.Persistence;

namespace PmsZafiro.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DashboardController : ControllerBase
{
    private readonly PmsDbContext _context;

    public DashboardController(PmsDbContext context)
    {
        _context = context;
    }

    [HttpGet("stats")]
    public async Task<ActionResult<DashboardStatsDto>> GetStats()
    {
        // Usamos DateTime.Now para evitar líos de UTC por ahora
        var today = DateTime.Now.Date; 
        var tomorrow = today.AddDays(1); 

        // 1. Métricas de Habitaciones
        var rooms = await _context.Rooms.ToListAsync();
        var totalRooms = rooms.Count;
        var occupiedCount = rooms.Count(r => r.Status == RoomStatus.Occupied);
        
        var occupancyRate = totalRooms > 0 
            ? (double)occupiedCount / totalRooms * 100 
            : 0;

        // 2. Métricas de Reservas
        // FIX: Usamos rangos (>= hoy Y < mañana) en lugar de .Date == today
        var checkInsPending = await _context.Reservations
            .CountAsync(r => r.CheckIn >= today && r.CheckIn < tomorrow && r.Status == ReservationStatus.Confirmed);

        var checkOutsPending = await _context.Reservations
            .CountAsync(r => r.CheckOut >= today && r.CheckOut < tomorrow && r.Status == ReservationStatus.CheckedIn);

        // 3. Métricas Financieras
        var totalRevenue = await _context.FolioTransactions
            .Where(t => t.Type == TransactionType.Payment && t.CreatedAt >= today && t.CreatedAt < tomorrow)
            .SumAsync(t => t.Amount);

        return Ok(new DashboardStatsDto
        {
            OccupancyRate = Math.Round(occupancyRate, 1),
            TotalRevenue = totalRevenue,
            CheckInsPending = checkInsPending,
            CheckOutsPending = checkOutsPending,
            RoomStatusCounts = new RoomStatusCountsDto 
            { 
                Clean = rooms.Count(r => r.Status == RoomStatus.Available),
                Dirty = rooms.Count(r => r.Status == RoomStatus.Dirty),
                Maintenance = rooms.Count(r => r.Status == RoomStatus.Maintenance),
                Occupied = occupiedCount
            }
        });
    }

    [HttpGet("revenue-history")]
    public async Task<ActionResult<List<RevenueChartDataDto>>> GetRevenueHistory()
    {
        var limitDate = DateTime.UtcNow.Date.AddDays(-7);

        // FIX POSTGRES: GroupBy por fecha en cliente o ajuste de query
        // Para evitar problemas de "DateDiff" o "DateTrunc" complejos, traemos los datos y agrupamos en memoria 
        // (Para 7 días de datos no hay impacto de rendimiento)
        var rawData = await _context.FolioTransactions
            .Where(t => t.CreatedAt >= limitDate && t.Type == TransactionType.Payment)
            .Select(t => new { t.CreatedAt, t.Amount }) // Proyección ligera
            .ToListAsync();

        var groupedData = rawData
            .GroupBy(t => t.CreatedAt.Date)
            .Select(g => new 
            {
                Date = g.Key,
                Total = g.Sum(t => t.Amount)
            })
            .ToList();

        var result = new List<RevenueChartDataDto>();
        for (int i = 6; i >= 0; i--)
        {
            var targetDate = DateTime.UtcNow.Date.AddDays(-i);
            var entry = groupedData.FirstOrDefault(d => d.Date == targetDate);
            
            var dayLabel = targetDate.ToString("ddd", new System.Globalization.CultureInfo("es-CO")); 
            dayLabel = char.ToUpper(dayLabel[0]) + dayLabel.Substring(1);

            result.Add(new RevenueChartDataDto
            {
                Name = dayLabel,
                Ingresos = entry?.Total ?? 0,
                Gastos = 0 
            });
        }

        return Ok(result);
    }

    // ... (El resto de métodos Demographics y RecentActivity pueden quedar igual)
    // Solo asegúrate de que RecentActivity no use DateDiffDay si lo tiene.
    
    [HttpGet("demographics")]
    public async Task<ActionResult<List<DemographicDataDto>>> GetDemographics()
    {
        var data = await _context.Guests
            .GroupBy(g => g.Nationality)
            .Select(g => new DemographicDataDto
            {
                Name = g.Key,
                Value = g.Count()
            })
            .OrderByDescending(x => x.Value)
            .Take(5)
            .ToListAsync();

        return Ok(data);
    }

    [HttpGet("recent-activity")]
    public async Task<ActionResult<List<ActivityFeedDto>>> GetRecentActivity()
    {
        var recentReservations = await _context.Reservations
            .Include(r => r.Guest)
            .OrderByDescending(r => r.CreatedAt)
            .Take(5)
            .ToListAsync();

        var activities = recentReservations.Select(res => new ActivityFeedDto
        {
            Id = res.Id.ToString(),
            User = $"{res.Guest.FirstName} {res.Guest.LastName}",
            Action = "Nueva Reserva",
            Time = res.CreatedAt.ToLocalTime().ToString("HH:mm"),
            Amount = $"+{res.TotalAmount:N0}",
            Avatar = "",
            Initials = (res.Guest.FirstName.Substring(0, 1) + res.Guest.LastName.Substring(0, 1)).ToUpper()
        }).ToList();

        return Ok(activities);
    }
}