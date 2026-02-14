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
    public string? CiudadDestino { get; set; } // Agregado por si acaso
    public string? SignatureBase64 { get; set; } 

    // Acompañantes
    public List<CompanionDto> Companions { get; set; } = new();
}

public class CompanionDto
{
    public string PrimerNombre { get; set; } = string.Empty;
    public string PrimerApellido { get; set; } = string.Empty;
    
    // --- CAMPOS QUE FALTABAN Y CAUSABAN EL ERROR CS1061 ---
    public string? SegundoNombre { get; set; }
    public string? SegundoApellido { get; set; }
    public string TipoId { get; set; } = "CC"; // Valor por defecto para evitar nulos
    // ------------------------------------------------------

    public string NumeroId { get; set; } = string.Empty;
    public string Nacionalidad { get; set; } = string.Empty;
}