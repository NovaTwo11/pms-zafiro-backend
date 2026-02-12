namespace PmsZafiro.Application.DTOs.Dashboard;

public class DashboardStatsDto
{
    /// <summary>
    /// Porcentaje de ocupación actual (0-100).
    /// </summary>
    public double OccupancyRate { get; set; }

    /// <summary>
    /// Ingresos totales generados hoy (Pagos).
    /// </summary>
    public decimal TotalRevenue { get; set; }

    /// <summary>
    /// Cantidad de llegadas pendientes para hoy.
    /// </summary>
    public int CheckInsPending { get; set; }

    /// <summary>
    /// Cantidad de salidas pendientes para hoy.
    /// </summary>
    public int CheckOutsPending { get; set; }

    /// <summary>
    /// Desglose del estado de las habitaciones.
    /// </summary>
    public RoomStatusCountsDto RoomStatusCounts { get; set; } = new();
}