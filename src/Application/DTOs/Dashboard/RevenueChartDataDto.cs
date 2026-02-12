namespace PmsZafiro.Application.DTOs.Dashboard;

public class RevenueChartDataDto
{
    /// <summary>
    /// Etiqueta del eje X (ej. "Lun", "Mar").
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Monto de ingresos para ese día.
    /// </summary>
    public decimal Ingresos { get; set; }

    /// <summary>
    /// Monto de gastos para ese día (opcional/futuro).
    /// </summary>
    public decimal Gastos { get; set; }
}