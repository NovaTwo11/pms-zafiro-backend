using PmsZafiro.Domain.Enums;

namespace PmsZafiro.Application.DTOs.Guests;

public class GuestDto
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    
    // Campos separados para facilitar la edición en el frontend
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    
    public string DocumentType { get; set; } = string.Empty;
    public string DocumentNumber { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Nationality { get; set; } = string.Empty;
    
    // Fecha de nacimiento (puede ser nula)
    public DateOnly? BirthDate { get; set; }

    // Estadísticas y Estado
    public int TotalStays { get; set; }
    public DateTime? LastStayDate { get; set; }
    public string CurrentStatus { get; set; } = "previous"; // "in-house" o "previous"
}