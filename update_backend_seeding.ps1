# update_backend_seeding.ps1

# Detectar ruta raíz (asume que estás en la carpeta del proyecto)
$CurrentLocation = Get-Location
$SrcPath = Join-Path $CurrentLocation "src"

if (-not (Test-Path $SrcPath)) {
    Write-Error "❌ No se encuentra la carpeta 'src' en $CurrentLocation. Asegúrate de ejecutar este script en la raíz del proyecto Backend (donde está la carpeta src)."
    exit
}

$EnumsPath = Join-Path $SrcPath "Domain/Enums/Enums.cs"
$ControllersPath = Join-Path $SrcPath "API/Controllers"
$SeedControllerPath = Join-Path $ControllersPath "SeedController.cs"

# 1. Actualizar Enums.cs con las nuevas categorías
$EnumsContent = @"
namespace PmsZafiro.Domain.Enums;

public enum RoomType
{
    Doble,
    Triple,
    Familiar,
    SuiteFamiliar
}

public enum RoomStatus
{
    Available,
    Occupied,
    Dirty,
    Maintenance
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
    Charge,   // Cargo a la habitación
    Payment,  // Pago del cliente
    Adjustment // Corrección
}
"@

Set-Content -Path $EnumsPath -Value $EnumsContent
Write-Host "✅ Enums.cs actualizado en: $EnumsPath" -ForegroundColor Green

# 2. Crear SeedController.cs
$SeedControllerContent = @"
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PmsZafiro.Domain.Entities;
using PmsZafiro.Domain.Enums;
using PmsZafiro.Infrastructure.Persistence;

namespace PmsZafiro.API.Controllers;

[ApiController]
[Route(""api/[controller]"")]
public class SeedController : ControllerBase
{
    private readonly PmsDbContext _context;

    public SeedController(PmsDbContext context)
    {
        _context = context;
    }

    [HttpPost(""init"")]
    public async Task<IActionResult> InitializeDatabase()
    {
        // 1. Limpiar datos existentes (Orden inverso por FKs)
        _context.Folios.RemoveRange(_context.Folios);
        _context.Reservations.RemoveRange(_context.Reservations);
        _context.Guests.RemoveRange(_context.Guests);
        _context.Rooms.RemoveRange(_context.Rooms);
        await _context.SaveChangesAsync();

        // 2. Crear 10 Habitaciones
        var rooms = new List<Room>();
        for (int i = 1; i <= 10; i++)
        {
            var type = i switch
            {
                <= 3 => RoomType.Doble,          // 1, 2, 3
                <= 6 => RoomType.Triple,          // 4, 5, 6
                <= 8 => RoomType.Familiar,        // 7, 8
                _ => RoomType.SuiteFamiliar       // 9, 10
            };

            rooms.Add(new Room
            {
                Number = $""10{i-1}"", // 100, 101, etc.
                Type = type,
                Price = GetPrice(type),
                Status = RoomStatus.Available,
                Capacity = GetCapacity(type),
                Floor = 1
            });
        }
        await _context.Rooms.AddRangeAsync(rooms);
        await _context.SaveChangesAsync();

        // 3. Crear 2 Huéspedes
        var guest1 = new Guest { FirstName = ""Juan"", LastName = ""Perez"", Email = ""juan.perez@test.com"", Phone = ""555-0101"", DocumentNumber = ""12345678"" };
        var guest2 = new Guest { FirstName = ""Maria"", LastName = ""Gomez"", Email = ""maria.gomez@test.com"", Phone = ""555-0102"", DocumentNumber = ""87654321"" };
        
        await _context.Guests.AddRangeAsync(guest1, guest2);
        await _context.SaveChangesAsync();

        // 4. Crear 1 Reserva Activa (Check-in realizado)
        // Usamos la habitación 100 (Doble)
        var checkInRoom = rooms.First();
        checkInRoom.Status = RoomStatus.Occupied; 

        var reservation = new Reservation
        {
            GuestId = guest1.Id,
            RoomId = checkInRoom.Id,
            CheckInDate = DateTime.UtcNow.AddDays(-1), 
            CheckOutDate = DateTime.UtcNow.AddDays(2), 
            Status = ReservationStatus.CheckedIn,
            Adults = 2,
            Children = 0,
            TotalPrice = checkInRoom.Price * 3,
            CreatedAt = DateTime.UtcNow
        };

        await _context.Reservations.AddAsync(reservation);
        await _context.SaveChangesAsync();

        // 5. Crear Folio para la reserva activa
        var folio = new Folio
        {
            ReservationId = reservation.Id,
            Status = FolioStatus.Open,
            CreatedAt = DateTime.UtcNow,
            TotalCharges = reservation.TotalPrice, 
            TotalPayments = 0
        };

        folio.Transactions = new List<FolioTransaction>
        {
            new FolioTransaction
            {
                Type = TransactionType.Charge,
                Amount = reservation.TotalPrice,
                Description = ""Cargo por Alojamiento (3 noches)"",
                Date = DateTime.UtcNow,
                Category = ""Accommodation""
            }
        };

        await _context.Folios.AddAsync(folio);
        await _context.SaveChangesAsync();

        return Ok(new 
        { 
            Message = ""Database Seeded Successfully"", 
            Stats = new { 
                Rooms = 10, 
                Guests = 2, 
                ActiveReservations = 1,
                ActiveFolios = 1
            } 
        });
    }

    private decimal GetPrice(RoomType type) => type switch
    {
        RoomType.Doble => 100m,
        RoomType.Triple => 150m,
        RoomType.Familiar => 200m,
        RoomType.SuiteFamiliar => 300m,
        _ => 100m
    };

    private int GetCapacity(RoomType type) => type switch
    {
        RoomType.Doble => 2,
        RoomType.Triple => 3,
        RoomType.Familiar => 4,
        RoomType.SuiteFamiliar => 5,
        _ => 2
    };
}
"@

Set-Content -Path $SeedControllerPath -Value $SeedControllerContent
Write-Host "✅ SeedController.cs creado exitosamente en $SeedControllerPath" -ForegroundColor Green
Write-Host "🚀 Backend listo. Compila y ejecuta /api/seed/init" -ForegroundColor Cyan