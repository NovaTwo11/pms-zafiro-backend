using System.Text.Json.Serialization;
using System.ComponentModel.DataAnnotations;
using PmsZafiro.Domain.Enums;

namespace PmsZafiro.Application.DTOs.Guests;

public class CreateGuestDto
{
    // Usamos JsonPropertyName para que coincida con lo que envía tu Frontend (camelCase)
    
    [JsonPropertyName("firstName")]
    public string FirstName { get; set; } = string.Empty; // Primer Nombre

    [JsonPropertyName("secondName")]
    public string? SecondName { get; set; } // Segundo Nombre (NUEVO)

    [JsonPropertyName("lastName")]
    public string LastName { get; set; } = string.Empty; // Primer Apellido

    [JsonPropertyName("secondLastName")]
    public string? SecondLastName { get; set; } // Segundo Apellido (NUEVO)

    [JsonPropertyName("documentType")]
    public string DocumentType { get; set; } = "CC";

    [JsonPropertyName("documentNumber")]
    public string DocumentNumber { get; set; } = string.Empty;

    [JsonPropertyName("email")]
    public string? Email { get; set; }

    [JsonPropertyName("phone")]
    public string? Phone { get; set; }

    [JsonPropertyName("nationality")]
    public string? Nationality { get; set; }

    [JsonPropertyName("cityOrigin")]
    public string? CityOfOrigin { get; set; } // (NUEVO)

    // Recibir como string "YYYY-MM-DD" para evitar problemas de zona horaria
    [JsonPropertyName("birthDate")]
    public string? BirthDate { get; set; }
}