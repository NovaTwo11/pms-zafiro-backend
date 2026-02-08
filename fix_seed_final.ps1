# fix_seed_final.ps1

# Detectar ruta raíz
$CurrentLocation = Get-Location
$SrcPath = Join-Path $CurrentLocation "src"

if (-not (Test-Path $SrcPath)) {
    Write-Error "❌ No se encuentra la carpeta 'src'. Ejecuta esto en la raíz del proyecto."
    exit
}

$SeedControllerPath = Join-Path $SrcPath "API/Controllers/SeedController.cs"

# Nuevo contenido corregido (Usando comillas simples para C# dentro del string)
$SeedControllerContent = @"
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PmsZafiro.Domain.Entities;
using PmsZafiro.Domain.Enums;
using PmsZafiro.Infrastructure.Persistence;

namespace PmsZafiro.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SeedController : ControllerBase
{
    private readonly PmsDbContext _context;

    public SeedController(PmsDbContext context)
    {
        _context = context;
    }

    [HttpPost("init")]
    public async Task<IActionResult> InitializeDatabase()
    {
        // 1. Limpiar datos existentes (Orden inverso por FKs)
        var transactions = await _context.Set<FolioTransaction>().ToListAsync();
        _context.RemoveRange(transactions);
        
        var folios = await _context.Folios.ToListAsync();
        _context.Folios.RemoveRange(folios);

        var reservations = await _context.Reservations.ToListAsync();
        _context.Reservations.RemoveRange(reservations);

        var guests = await _context.Guests.ToListAsync();
        _context.Guests.RemoveRange(guests);

        var rooms = await _context.Rooms.ToListAsync();
        _context.Rooms.RemoveRange(rooms);
        
        await _context.SaveChangesAsync();

        // 2. Crear 10 Habitaciones
        var roomsList = new List<Room>();
        for (int i = 1; i <= 10; i++)
        {
            string category;
            decimal price;

            if (i <= 3) { category = "Doble"; price = 100m; }
            else if (i <= 6) { category = "Triple"; price = 150m; }
            else if (i <= 8) { category = "Familiar"; price = 200m; }
            else { category = "SuiteFamiliar"; price = 300m; }

            roomsList.Add(new Room
            {
                Id = Guid.NewGuid(),
                Number = $"10{i-1}",
                Category = category,
                BasePrice = price,
                Status = RoomStatus.Available
            });
        }
        await _context.Rooms.AddRangeAsync(roomsList);
        await _context.SaveChangesAsync();

        // 3. Crear 2 Huéspedes (Corregido según tu entidad Guest)
        var guest1 = new Guest 
        { 
            Id = Guid.NewGuid(),
            FirstName = "Juan", 
            LastName = "Perez", 
            Email = "juan.perez@test.com", 
            Phone = "555-0101", 
            DocumentNumber = "12345678",
            DocumentType = IdType.CC, // Corregido: IdType -> DocumentType
            Nationality = "Colombia", // Corregido: Country -> Nationality
            CreatedAt = DateTimeOffset.UtcNow
        };

        var guest2 = new Guest 
        { 
            Id = Guid.NewGuid(),
            FirstName = "Maria", 
            LastName = "Gomez", 
            Email = "maria.gomez@test.com", 
            Phone = "555-0102", 
            DocumentNumber = "87654321",
            DocumentType = IdType.CC,
            Nationality = "Colombia",
            CreatedAt = DateTimeOffset.UtcNow
        };
        
        await _context.Guests.AddRangeAsync(guest1, guest2);
        await _context.SaveChangesAsync();

        // 4. Crear 1 Reserva Activa
        var checkInRoom = roomsList.First();
        checkInRoom.Status = RoomStatus.Occupied;

        var reservation = new Reservation
        {
            Id = Guid.NewGuid(),
            ConfirmationCode = "RES-" + new Random().Next(1000, 9999),
            GuestId = guest1.Id,
            RoomId = checkInRoom.Id,
            CheckIn = DateTime.UtcNow.AddDays(-1),
            CheckOut = DateTime.UtcNow.AddDays(2),
            Status = ReservationStatus.CheckedIn,
            Adults = 2,
            Children = 0,
            TotalAmount = checkInRoom.BasePrice * 3,
            CreatedAt = DateTimeOffset.UtcNow
        };

        await _context.Reservations.AddAsync(reservation);
        await _context.SaveChangesAsync();

        // 5. Crear Folio
        var folio = new GuestFolio
        {
            Id = Guid.NewGuid(),
            ReservationId = reservation.Id,
            Status = FolioStatus.Open
        };

        var transaction = new FolioTransaction
        {
            Id = Guid.NewGuid(),
            FolioId = folio.Id,
            Type = TransactionType.Charge,
            Amount = reservation.TotalAmount,
            Description = "Cargo por Alojamiento (3 noches)",
            Quantity = 1,
            UnitPrice = reservation.TotalAmount,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedByUserId = "SEED-SYSTEM"
        };

        folio.Transactions.Add(transaction);

        await _context.Folios.AddAsync(folio);
        await _context.SaveChangesAsync();

        return Ok(new 
        { 
            Message = "Database Seeded Successfully v3", 
            Stats = new { 
                Rooms = 10, 
                Guests = 2, 
                ActiveReservations = 1,
                ActiveFolios = 1
            } 
        });
    }
}
"@

Set-Content -Path $SeedControllerPath -Value $SeedControllerContent
Write-Host "✅ SeedController.cs corregido sin errores de compilación." -ForegroundColor Green
Write-Host "🚀 Ejecuta 'dotnet build' y luego 'dotnet run'." -ForegroundColor Cyan