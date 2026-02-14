namespace PmsZafiro.Domain.Enums;

// --- Nuevas Categorías (Requerimiento) ---
public enum RoomType
{
    Doble,
    Triple,
    Familiar,
    SuiteFamiliar,
    Suite
}

// --- Enums Restaurados (Para corregir errores de compilación) ---
public enum IdType
{
    CC, // Cédula de Ciudadanía
    CE, // Cédula de Extranjería
    PA, // Pasaporte
    TI, // Tarjeta de Identidad
    RC  // Registro Civil
}

public enum NotificationType
{
    Info,
    Success,
    Warning,
    Error
}

public enum CashierShiftStatus
{
    Open = 0,
    Closed = 1
}

// --- Enums Generales ---
public enum RoomStatus
{
    Available,
    Occupied,
    Dirty,
    Maintenance,
    TouchUp,
    Blocked
}

public enum ReservationStatus
{
    Pending,
    Confirmed,
    CheckedIn,
    CheckedOut,
    Cancelled,
    NoShow,
    Blocked
}

public enum PaymentMethod
{
    // Definimos explícitamente los índices para evitar errores de desplazamiento
    None = 0,        // Para Cargos (Charges) que no implican pago inmediato
    Cash = 1,        // Efectivo
    CreditCard = 2,  // Tarjeta Crédito
    DebitCard = 3,   // Tarjeta Débito
    Transfer = 4,    // Transferencia
    Other = 5        // Otros
}

public enum FolioStatus
{
    Open,
    Closed,
    Cancelled
}

public enum TransactionType
{
    Charge = 0,   // Cargo a habitación
    Payment = 1,  // Pago de cliente (Ingreso)
    Expense = 2,  // Gasto/Egreso de caja (Salida) <--- NECESARIO
    Income = 3    // Ingreso manual a caja (Entrada extra) <--- NECESARIO
}

