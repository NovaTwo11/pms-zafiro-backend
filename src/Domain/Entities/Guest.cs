using PmsZafiro.Domain.Enums;

namespace PmsZafiro.Domain.Entities;

public class Guest
{
    public Guid Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string FullName => $"{FirstName} {LastName}";
    
    public IdType DocumentType { get; set; }
    public string DocumentNumber { get; set; } = string.Empty;
    
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Nationality { get; set; } = string.Empty;
    public DateOnly? BirthDate { get; set; }
    
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<Reservation> Reservations { get; set; } = new List<Reservation>();
}
