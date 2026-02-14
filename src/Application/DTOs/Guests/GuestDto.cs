using PmsZafiro.Domain.Enums;

namespace PmsZafiro.Application.DTOs.Guests;

public class GuestDto
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    
    public string DocumentType { get; set; } = string.Empty;
    public string DocumentNumber { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Nationality { get; set; } = string.Empty;
    
    // --- CORRECCIÓN: Faltaba esta propiedad ---
    public string? CityOfOrigin { get; set; } 
    // ------------------------------------------
    
    public DateOnly? BirthDate { get; set; }

    public int TotalStays { get; set; }
    public DateTime? LastStayDate { get; set; }
    public string CurrentStatus { get; set; } = "previous"; 
}