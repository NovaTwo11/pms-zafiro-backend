using System.ComponentModel.DataAnnotations;

namespace PmsZafiro.Application.DTOs.Guests;

public class UpdateCheckInGuestDto
{
    // Datos del Titular
    [Required]
    public string Nacionalidad { get; set; } = string.Empty;
    
    [Required]
    public string TipoId { get; set; } = string.Empty;
    
    [Required]
    public string NumeroId { get; set; } = string.Empty;
    
    [Required]
    public string PrimerNombre { get; set; } = string.Empty;
    
    [Required]
    public string PrimerApellido { get; set; } = string.Empty;
    
    public string? SegundoNombre { get; set; }
    public string? SegundoApellido { get; set; }
    public string? Telefono { get; set; }
    public string? Correo { get; set; }
    public string? Direccion { get; set; }
    public string? CiudadOrigen { get; set; }
    
    // Acompañantes
    public List<CompanionDto> Companions { get; set; } = new();
}

public class CompanionDto
{
    public string PrimerNombre { get; set; } = string.Empty;
    public string PrimerApellido { get; set; } = string.Empty;
    public string NumeroId { get; set; } = string.Empty;
    public string Nacionalidad { get; set; } = string.Empty;
}