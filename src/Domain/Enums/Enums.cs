namespace PmsZafiro.Domain.Enums;

public enum RoomStatus
{
    Available = 0,   // Limpia
    Occupied = 1,    // Ocupada (Por sistema)
    Dirty = 2,       // Sucia
    Maintenance = 3, // Mantenimiento (No bloquea venta, solo aviso)
    TouchUp = 4,     // Retoque (No sucia del todo, pero requiere inspección)
    Blocked = 5      // Bloqueo duro (Solo desde calendario/admin)
}
    public enum ReservationStatus { Pending, Confirmed, CheckedIn, CheckedOut, Cancelled, NoShow }
public enum FolioStatus { Open, Closed } // <--- AGREGAR ESTO
public enum IdType { CC, CE, PA, TI, RC }
public enum TransactionType { Charge, Payment, Adjustment }
public enum NotificationType { Info, Success, Warning, Error }