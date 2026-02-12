using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PmsZafiro.Application.DTOs.Dashboard;
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

    /// <summary>
    /// Obtiene las métricas principales (KPIs) y estado de limpieza para el dashboard.
    /// </summary>
    [HttpGet("stats")]
    public async Task<ActionResult<DashboardStatsDto>> GetStats()
    {
        var today = DateTime.UtcNow.Date; // Usar UTC para consistencia en la nube

        // 1. Métricas de Habitaciones
        var rooms = await _context.Rooms.ToListAsync();
        var totalRooms = rooms.Count;
        var occupiedCount = rooms.Count(r => r.Status == RoomStatus.Occupied);
        
        var occupancyRate = totalRooms > 0 
            ? (double)occupiedCount / totalRooms * 100 
            : 0;

        // 2. Métricas de Reservas (Operatividad Diaria)
        var checkInsPending = await _context.Reservations
            .CountAsync(r => r.CheckIn.Date == today && r.Status == ReservationStatus.Confirmed);

        var checkOutsPending = await _context.Reservations
            .CountAsync(r => r.CheckOut.Date == today && r.Status == ReservationStatus.CheckedIn);

        // 3. Métricas Financieras (Ingresos del día)
        // Se suman las transacciones de tipo 'Payment' creadas hoy
        var totalRevenue = await _context.Set<PmsZafiro.Domain.Entities.FolioTransaction>()
            .Where(t => t.Type == TransactionType.Payment && t.CreatedAt.Date == today)
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

    /// <summary>
    /// Obtiene el historial de ingresos de los últimos 7 días para el gráfico.
    /// </summary>
    [HttpGet("revenue-history")]
    public async Task<ActionResult<List<RevenueChartDataDto>>> GetRevenueHistory()
    {
        var limitDate = DateTime.UtcNow.AddDays(-7).Date;

        // Agrupar pagos por fecha
        var dbData = await _context.Set<PmsZafiro.Domain.Entities.FolioTransaction>()
            .Where(t => t.CreatedAt >= limitDate && t.Type == TransactionType.Payment)
            .GroupBy(t => t.CreatedAt.Date)
            .Select(g => new 
            {
                Date = g.Key,
                Total = g.Sum(t => t.Amount)
            })
            .ToListAsync();

        // Rellenar días sin ventas para que el gráfico no tenga huecos
        var result = new List<RevenueChartDataDto>();
        for (int i = 6; i >= 0; i--)
        {
            var targetDate = DateTime.UtcNow.Date.AddDays(-i);
            var entry = dbData.FirstOrDefault(d => d.Date == targetDate);
            
            // Mapeo cultural para días (ej. "Mon" o "Lun" dependiendo del servidor)
            // Para forzar español se podría usar CultureInfo("es-ES")
            var dayLabel = targetDate.ToString("ddd", new System.Globalization.CultureInfo("es-CO")); 
            // Pone la primera letra en mayúscula
            dayLabel = char.ToUpper(dayLabel[0]) + dayLabel.Substring(1);

            result.Add(new RevenueChartDataDto
            {
                Name = dayLabel,
                Ingresos = entry?.Total ?? 0,
                Gastos = 0 // Placeholder para futura implementación de gastos
            });
        }

        return Ok(result);
    }

    /// <summary>
    /// Obtiene la distribución demográfica de los huéspedes (Top 5 nacionalidades).
    /// </summary>
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

    /// <summary>
    /// Obtiene un feed de actividades recientes (nuevas reservas, check-ins, etc).
    /// </summary>
    [HttpGet("recent-activity")]
    public async Task<ActionResult<List<ActivityFeedDto>>> GetRecentActivity()
    {
        // Ejemplo: Obtenemos las últimas 5 reservas creadas
        var recentReservations = await _context.Reservations
            .Include(r => r.Guest)
            .OrderByDescending(r => r.CreatedAt)
            .Take(5)
            .ToListAsync();

        var activities = recentReservations.Select(res => new ActivityFeedDto
        {
            Id = res.Id.ToString(),
            User = $"{res.Guest.FirstName} {res.Guest.LastName}",
            Action = "Nueva Reserva", // Podríamos variar esto según el estado
            Time = res.CreatedAt.ToLocalTime().ToString("HH:mm"),
            Amount = $"+{res.TotalAmount:N0}", // Formato numérico simple
            Avatar = "",
            Initials = (res.Guest.FirstName.Substring(0, 1) + res.Guest.LastName.Substring(0, 1)).ToUpper()
        }).ToList();

        return Ok(activities);
    }
}