namespace PmsZafiro.Domain.Enums;

public enum RoomStatus { Available, Occupied, Dirty, Maintenance, Blocked }
public enum ReservationStatus { Pending, Confirmed, CheckedIn, CheckedOut, Cancelled, NoShow }
public enum FolioStatus { Open, Closed } // <--- AGREGAR ESTO
public enum IdType { CC, CE, PA, TI, RC }
public enum TransactionType { Charge, Payment, Adjustment }
public enum NotificationType { Info, Success, Warning, Error }