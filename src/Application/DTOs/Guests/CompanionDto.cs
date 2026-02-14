using System.Text.Json.Serialization;

namespace PmsZafiro.Application.DTOs.Guests;

public class CompanionDto
{
    [JsonPropertyName("primerNombre")]
    public string PrimerNombre { get; set; } = string.Empty;

    [JsonPropertyName("segundoNombre")]
    public string? SegundoNombre { get; set; }

    [JsonPropertyName("primerApellido")]
    public string PrimerApellido { get; set; } = string.Empty;

    [JsonPropertyName("segundoApellido")]
    public string? SegundoApellido { get; set; }

    [JsonPropertyName("tipoId")]
    public string TipoId { get; set; } = "CC";

    [JsonPropertyName("numeroId")]
    public string NumeroId { get; set; } = string.Empty;

    [JsonPropertyName("nacionalidad")]
    public string Nacionalidad { get; set; } = string.Empty;
}