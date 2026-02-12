namespace PmsZafiro.Application.DTOs.Dashboard;

public class DemographicDataDto
{
    /// <summary>
    /// Nombre de la nacionalidad o segmento.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Cantidad de huéspedes.
    /// </summary>
    public int Value { get; set; }
}