# --- Configuración Inicial ---
$SolutionName = "PmsZafiro"
$BaseDir = Get-Location

Write-Host "Iniciando generación de arquitectura PMS Zafiro..." -ForegroundColor Cyan

# 1. Crear Solución
dotnet new sln -n $SolutionName

# 2. Crear Capas (Proyectos)
# Domain: Entidades y lógica pura (Sin dependencias externas)
dotnet new classlib -n "$SolutionName.Domain" -o src/Domain
# Application: Casos de uso, DTOs, Interfaces
dotnet new classlib -n "$SolutionName.Application" -o src/Application
# Infrastructure: Base de datos, correos, archivos
dotnet new classlib -n "$SolutionName.Infrastructure" -o src/Infrastructure
# API: Controladores REST
dotnet new webapi -n "$SolutionName.API" -o src/API

# 3. Agregar Proyectos a la Solución
dotnet sln add src/Domain/"$SolutionName.Domain.csproj"
dotnet sln add src/Application/"$SolutionName.Application.csproj"
dotnet sln add src/Infrastructure/"$SolutionName.Infrastructure.csproj"
dotnet sln add src/API/"$SolutionName.API.csproj"

# 4. Establecer Referencias entre Proyectos (Clean Architecture)
# Application usa Domain
dotnet add src/Application reference src/Domain
# Infrastructure usa Application (para implementar interfaces) y Domain
dotnet add src/Infrastructure reference src/Application
dotnet add src/Infrastructure reference src/Domain
# API usa Application (para enviar comandos) y Infrastructure (para inyección de dependencias)
dotnet add src/API reference src/Application
dotnet add src/API reference src/Infrastructure

# 5. Instalar Paquetes NuGet Básicos (Entity Framework Core)
Write-Host "Instalando Entity Framework Core..." -ForegroundColor Yellow
dotnet add src/Infrastructure package Microsoft.EntityFrameworkCore.SqlServer
dotnet add src/Infrastructure package Microsoft.EntityFrameworkCore.Tools
dotnet add src/API package Microsoft.EntityFrameworkCore.Design

# --- GENERACIÓN DE CÓDIGO (ENTIDADES) ---
Write-Host "Generando Entidades del Dominio..." -ForegroundColor Cyan

$EntitiesPath = "$BaseDir/src/Domain/Entities"
$EnumsPath = "$BaseDir/src/Domain/Enums"
New-Item -ItemType Directory -Force -Path $EntitiesPath | Out-Null
New-Item -ItemType Directory -Force -Path $EnumsPath | Out-Null

# --- ENUMS ---

$ContentEnumStatus = @"
namespace $SolutionName.Domain.Enums;

public enum RoomStatus { Available, Occupied, Dirty, Maintenance, Blocked }
public enum ReservationStatus { Pending, Confirmed, CheckedIn, CheckedOut, Cancelled, NoShow }
public enum IdType { CC, CE, PA, TI, RC } // Basado en tu TS
public enum TransactionType { Charge, Payment, Adjustment }
public enum NotificationType { Info, Success, Warning, Error }
"@
Set-Content -Path "$EnumsPath/Enums.cs" -Value $ContentEnumStatus

# --- ENTITY: GUEST ---
$ContentGuest = @"
using $SolutionName.Domain.Enums;

namespace $SolutionName.Domain.Entities;

public class Guest
{
    public Guid Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string FullName => `$"{FirstName} {LastName}";
    
    public IdType DocumentType { get; set; }
    public string DocumentNumber { get; set; } = string.Empty;
    
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Nationality { get; set; } = string.Empty;
    public DateOnly? BirthDate { get; set; }
    
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
"@
Set-Content -Path "$EntitiesPath/Guest.cs" -Value $ContentGuest

# --- ENTITY: ROOM ---
$ContentRoom = @"
using $SolutionName.Domain.Enums;

namespace $SolutionName.Domain.Entities;

public class Room
{
    public Guid Id { get; set; }
    public string Number { get; set; } = string.Empty;
    public string Category { get; set; } = "Standard"; // Podría ser Enum
    public decimal BasePrice { get; set; }
    public RoomStatus Status { get; set; }
    
    // Relación: Precios personalizados por fecha
    public ICollection<RoomPriceOverride> PriceOverrides { get; set; } = new List<RoomPriceOverride>();
}

public class RoomPriceOverride
{
    public Guid Id { get; set; }
    public Guid RoomId { get; set; }
    public DateOnly Date { get; set; }
    public decimal Price { get; set; }
}
"@
Set-Content -Path "$EntitiesPath/Room.cs" -Value $ContentRoom

# --- ENTITY: RESERVATION ---
$ContentReservation = @"
using $SolutionName.Domain.Enums;

namespace $SolutionName.Domain.Entities;

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
    public Guid GuestId { get; set; }
    public Guest Guest { get; set; } = null!;
    
    public bool IsPrimary { get; set; }
    public string OriginCity { get; set; } = string.Empty;
    public string OriginCountry { get; set; } = string.Empty;
}
"@
Set-Content -Path "$EntitiesPath/Reservation.cs" -Value $ContentReservation

# --- ENTITY: FOLIO & TRANSACTIONS (Lógica Financiera) ---
$ContentFolio = @"
using $SolutionName.Domain.Enums;

namespace $SolutionName.Domain.Entities;

public abstract class Folio
{
    public Guid Id { get; set; }
    public bool IsClosed { get; set; }
    
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
"@
Set-Content -Path "$EntitiesPath/Folio.cs" -Value $ContentFolio

# --- ENTITY: NOTIFICATION (Tu requerimiento específico) ---
$ContentNotif = @"
using $SolutionName.Domain.Enums;

namespace $SolutionName.Domain.Entities;

public class AppNotification
{
    public Guid Id { get; set; }
    
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public NotificationType Type { get; set; } // Info, Warning, Error
    
    public string? TargetRole { get; set; } // Ej: 'Admin', 'Reception'
    public string? TargetUserId { get; set; } // Si es para alguien específico
    
    public bool IsRead { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    
    // Navegación: Al hacer click, ¿a dónde va?
    // Ej: '/reservas/123-abc' o '/folios/555'
    public string? ActionUrl { get; set; } 
    
    // Opcional: Relacionar con entidad para integridad
    public Guid? RelatedEntityId { get; set; }
    public string? RelatedEntityType { get; set; } // 'Reservation', 'Folio'
}
"@
Set-Content -Path "$EntitiesPath/AppNotification.cs" -Value $ContentNotif

# --- Limpiar archivos por defecto ---
Remove-Item src/Domain/Class1.cs -ErrorAction SilentlyContinue
Remove-Item src/Application/Class1.cs -ErrorAction SilentlyContinue
Remove-Item src/Infrastructure/Class1.cs -ErrorAction SilentlyContinue

Write-Host "¡PROYECTO GENERADO CON ÉXITO!" -ForegroundColor Green
Write-Host "1. Abre Rider."
Write-Host "2. Selecciona 'Open' y busca el archivo: $BaseDir/$SolutionName.sln"
Write-Host "3. Disfruta de tu Clean Architecture."