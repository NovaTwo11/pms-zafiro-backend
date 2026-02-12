using System.ComponentModel.DataAnnotations.Schema;
using PmsZafiro.Domain.Enums;

namespace PmsZafiro.Domain.Entities;

public abstract class Folio
{
    public Guid Id { get; set; }
    public FolioStatus Status { get; set; } = FolioStatus.Open;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow; 

    public ICollection<FolioTransaction> Transactions { get; set; } = new List<FolioTransaction>();
    
    // Balance: Cargos (Debe) - Pagos (Haber)
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
    public string Alias { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

public class FolioTransaction
{
    public Guid Id { get; set; }
    
    // CAMBIO IMPORTANTE: Hacemos FolioId anulable para permitir movimientos de caja sin folio
    public Guid? FolioId { get; set; }
    public Folio? Folio { get; set; }

    public TransactionType Type { get; set; }
    public string Description { get; set; } = string.Empty;
    
    public decimal Amount { get; set; }
    public int Quantity { get; set; } = 1;
    public decimal UnitPrice { get; set; }
    
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.None;
    
    public Guid? CashierShiftId { get; set; }
    public CashierShift? CashierShift { get; set; }
    
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string CreatedByUserId { get; set; } = string.Empty;
}