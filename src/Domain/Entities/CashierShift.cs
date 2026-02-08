using PmsZafiro.Domain.Enums;
namespace PmsZafiro.Domain.Entities;

public class CashierShift
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public DateTimeOffset OpenedAt { get; set; }
    public DateTimeOffset? ClosedAt { get; set; }
    public decimal StartingAmount { get; set; }
    public decimal SystemCalculatedAmount { get; set; }
    public decimal ActualAmount { get; set; }
    public CashierShiftStatus Status { get; set; } = CashierShiftStatus.Open;
    public ICollection<FolioTransaction> Transactions { get; set; } = new List<FolioTransaction>();
}