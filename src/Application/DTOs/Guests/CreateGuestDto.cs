using PmsZafiro.Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace PmsZafiro.Application.DTOs.Guests;

public class CreateGuestDto
{
    [Required] 
    public string FirstName { get; set; } = string.Empty;
    
    [Required] 
    public string LastName { get; set; } = string.Empty;
    
    [Required] 
    public IdType DocumentType { get; set; }
    
    [Required] 
    public string DocumentNumber { get; set; } = string.Empty;
    
    [EmailAddress] 
    public string Email { get; set; } = string.Empty;
    
    public string Phone { get; set; } = string.Empty;
    
    public string Nationality { get; set; } = string.Empty;
    
    public DateOnly? BirthDate { get; set; }
}