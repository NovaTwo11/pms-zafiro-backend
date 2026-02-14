using System.Text.Json.Serialization;
using System.ComponentModel.DataAnnotations;

namespace PmsZafiro.Application.DTOs.Guests;

public class UpdateCheckInGuestDto
{
    // === Datos del Titular ===

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

    [JsonPropertyName("telefono")]
    public string? Telefono { get; set; }

    [JsonPropertyName("correo")]
    public string? Correo { get; set; }
    [JsonPropertyName("ciudadOrigen")]
    public string? CiudadOrigen { get; set; }

    [JsonPropertyName("fechaNacimiento")]
    public string? FechaNacimiento { get; set; }

    // === NUEVAS PROPIEDADES (Las que causaban el error) ===

    [JsonPropertyName("signatureBase64")]
    public string? SignatureBase64 { get; set; }

    [JsonPropertyName("companions")]
    public List<CompanionDto>? Companions { get; set; } = new();
}