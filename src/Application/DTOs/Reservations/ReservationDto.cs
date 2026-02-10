using PmsZafiro.Domain.Enums;

namespace PmsZafiro.Application.DTOs.Reservations;

public class ReservationDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    
    // Datos aplanados para facilitar el frontend
    public Guid MainGuestId { get; set; }
    public string MainGuestName { get; set; } = string.Empty;
    
    public Guid RoomId { get; set; }
    public string RoomNumber { get; set; } = string.Empty;
    
    public DateTime CheckIn { get; set; }
    public DateTime CheckOut { get; set; }
    public int Nights { get; set; }
    
    // Para saber si ya tiene folio (casi siempre sí)
    public bool HasFolio { get; set; }
}
