using System.Text.Json.Serialization;

namespace PmsZafiro.Application.DTOs.Folios;
    

public class FolioDto
{
    public Guid Id { get; set; }
    
    // Campo CRÍTICO para que el botón de check-out no congele la pantalla
    public Guid? ReservationId { get; set; } 
    
    public string Status { get; set; } = string.Empty;
    
    [JsonPropertyName("type")]
    public string FolioType { get; set; } = string.Empty; 
    
    public decimal Balance { get; set; }
    public decimal TotalCharges { get; set; }
    public decimal TotalPayments { get; set; }
    public DateTime CreatedAt { get; set; }

    // --- Datos de Huésped ---
    public string? GuestName { get; set; }
    public string? RoomNumber { get; set; }
    public DateTime? CheckIn { get; set; }
    public DateTime? CheckOut { get; set; }
    public int Nights { get; set; }

    // --- Datos de Externo ---
    public string? Alias { get; set; }
    public string? Description { get; set; }

    public List<FolioTransactionDto> Transactions { get; set; } = new();
}