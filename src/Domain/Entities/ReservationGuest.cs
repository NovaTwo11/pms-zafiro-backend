using System;

namespace PmsZafiro.Domain.Entities;

public class ReservationGuest
{
    public Guid ReservationId { get; set; }
    public Reservation Reservation { get; set; } = null!;

    public Guid GuestId { get; set; }
    public Guest Guest { get; set; } = null!;

    // Opcional: Para saber si este acompañante ya firmó o hizo check-in individual
    public bool IsCheckedIn { get; set; } = false;
    
    public bool IsPrincipal { get; set; }
}