using PmsZafiro.Domain.Enums;

namespace PmsZafiro.Domain.Entities;

public abstract class Folio
{
    public Guid Id { get; set; }
    public FolioStatus Status { get; set; } = FolioStatus.Open;    
    public ICollection<FolioTransaction> Transactions { get; set; } = new List<FolioTransaction>();
    
    // Saldo calculado
    public decimal Balance => Transactions
        .Where(t => t.Type == TransactionType.Charge)
        .Sum(t => t.Amount) - 
        Transactions
        .Where(t => t.Type == TransactionType.Payment)
        .Sum(t => t.Amount);
}

public class GuestFolio : Folio
{
    public Guid ReservationId { get; set; }
    public Reservation Reservation { get; set; } = null!;
}

public class ExternalFolio : Folio
{
    public string Alias { get; set; } = string.Empty; // Ej: Evento Boda
    public string Description { get; set; } = string.Empty;
}

public class FolioTransaction
{
    public Guid Id { get; set; }
    public Guid FolioId { get; set; }
    
    public TransactionType Type { get; set; } // Charge, Payment
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; } // Siempre positivo
    public int Quantity { get; set; } = 1;
    public decimal UnitPrice { get; set; }
    
    // Auditoría detallada
    public DateTimeOffset CreatedAt { get; set; }
    public string CreatedByUserId { get; set; } = string.Empty; // ID del empleado
}
