# fix_enums.ps1

# Detectar ruta src
$CurrentLocation = Get-Location
$SrcPath = Join-Path $CurrentLocation "src"

if (-not (Test-Path $SrcPath)) {
    Write-Error "❌ No se encuentra la carpeta 'src'. Ejecuta esto en la raíz del proyecto."
    exit
}

$EnumsPath = Join-Path $SrcPath "Domain/Enums/Enums.cs"

# Contenido corregido con TODOS los Enums necesarios
$EnumsContent = @"
namespace PmsZafiro.Domain.Enums;

// --- Nuevas Categorías (Requerimiento) ---
public enum RoomType
{
    Doble,
    Triple,
    Familiar,
    SuiteFamiliar
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
    Open,
    Closed
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
    NoShow
}

public enum PaymentMethod
{
    None = 0,
    Cash,
    CreditCard,
    DebitCard,
    Transfer,
    Other
}

public enum FolioStatus
{
    Open,
    Closed,
    Cancelled
}

public enum TransactionType
{
    Charge,
    Payment,
    Adjustment
}
"@

Set-Content -Path $EnumsPath -Value $EnumsContent
Write-Host "✅ Enums.cs reparado exitosamente en: $EnumsPath" -ForegroundColor Green
Write-Host "🔄 Ahora intenta compilar de nuevo con 'dotnet build'." -ForegroundColor Cyan