namespace PmsZafiro.Domain.Entities;

public class User
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty; // Nunca guardes texto plano
    public string Role { get; set; } = "Staff"; // Staff, Admin
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}