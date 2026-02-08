using PmsZafiro.Domain.Enums;

namespace PmsZafiro.Domain.Entities;

public class Reservation
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty; // Código corto humano (ej: RES-492)
    
    public Guid MainGuestId { get; set; }
    public Guest MainGuest { get; set; } = null!;
    
    public Guid RoomId { get; set; }
    public Room Room { get; set; } = null!;
    
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public int Nights => EndDate.DayNumber - StartDate.DayNumber;
    
    public ReservationStatus Status { get; set; }
    
    // Auditoría
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    
    public ICollection<ReservationGuestDetail> Guests { get; set; } = new List<ReservationGuestDetail>();
}

// Tabla intermedia con datos del viaje
public class ReservationGuestDetail
{
    public Guid ReservationId { get; set; }
    public Reservation Reservation { get; set; } = null!;
    public Guid GuestId { get; set; }
    public Guest Guest { get; set; } = null!;
    
    public bool IsPrimary { get; set; }
    public string OriginCity { get; set; } = string.Empty;
    public string OriginCountry { get; set; } = string.Empty;
}
