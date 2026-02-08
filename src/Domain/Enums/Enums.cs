namespace PmsZafiro.Domain.Enums;

public enum RoomStatus { Available = 0, Occupied = 1, Dirty = 2, Maintenance = 3, TouchUp = 4, Blocked = 5 }
public enum ReservationStatus { Pending, Confirmed, CheckedIn, CheckedOut, Cancelled, NoShow }
public enum FolioStatus { Open, Closed }
public enum IdType { CC, CE, PA, TI, RC }
public enum TransactionType { Charge, Payment, Adjustment }
public enum NotificationType { Info, Success, Warning, Error }
public enum PaymentMethod { None = 0, Cash = 1, CreditCard = 2, DebitCard = 3, Transfer = 4 }
public enum CashierShiftStatus { Open, Closed }