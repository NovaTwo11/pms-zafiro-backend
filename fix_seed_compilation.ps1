# fix_seed_compilation.ps1

# Detectar ruta raíz
$CurrentLocation = Get-Location
$SrcPath = Join-Path $CurrentLocation "src"

if (-not (Test-Path $SrcPath)) {
    Write-Error "❌ No se encuentra la carpeta 'src'. Ejecuta esto en la raíz del proyecto."
    exit
}

$SeedControllerPath = Join-Path $SrcPath "API/Controllers/SeedController.cs"

# Nuevo contenido corregido basado en TUS archivos
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
        // Borramos transacciones primero
        var transactions = await _context.Set<FolioTransaction>().ToListAsync();
        _context.RemoveRange(transactions);
        
        // Borramos folios (GuestFolio y ExternalFolio)
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
            // Definir tipo basado en índice
            string category;
            decimal price;

            if (i <= 3) { category = ""Doble""; price = 100m; }
            else if (i <= 6) { category = ""Triple""; price = 150m; }
            else if (i <= 8) { category = ""Familiar""; price = 200m; }
            else { category = ""SuiteFamiliar""; price = 300m; }

            roomsList.Add(new Room
            {
                Id = Guid.NewGuid(),
                Number = $""10{i-1}"", // 100, 101...
                Category = category,   // Tu entidad usa string
                BasePrice = price,     // Tu entidad usa BasePrice
                Status = RoomStatus.Available
            });
        }
        await _context.Rooms.AddRangeAsync(roomsList);
        await _context.SaveChangesAsync();

        // 3. Crear 2 Huéspedes
        var guest1 = new Guest 
        { 
            Id = Guid.NewGuid(),
            FirstName = ""Juan"", 
            LastName = ""Perez"", 
            Email = ""juan.perez@test.com"", 
            Phone = ""555-0101"", 
            DocumentNumber = ""12345678"",
            IdType = IdType.CC, // Asumiendo que IdType existe en Guest
            Address = ""Calle Falsa 123"",
            City = ""Bogota"",
            Country = ""Colombia"",
            CreatedAt = DateTimeOffset.UtcNow
        };

        var guest2 = new Guest 
        { 
            Id = Guid.NewGuid(),
            FirstName = ""Maria"", 
            LastName = ""Gomez"", 
            Email = ""maria.gomez@test.com"", 
            Phone = ""555-0102"", 
            DocumentNumber = ""87654321"",
            IdType = IdType.CC,
            Address = ""Carrera 45"",
            City = ""Medellin"",
            Country = ""Colombia"",
            CreatedAt = DateTimeOffset.UtcNow
        };
        
        await _context.Guests.AddRangeAsync(guest1, guest2);
        await _context.SaveChangesAsync();

        // 4. Crear 1 Reserva Activa (Check-in realizado) en la Habitación 100
        var checkInRoom = roomsList.First();
        checkInRoom.Status = RoomStatus.Occupied; // Actualizar estado cuarto

        var reservation = new Reservation
        {
            Id = Guid.NewGuid(),
            ConfirmationCode = ""RES-"" + new Random().Next(1000, 9999),
            GuestId = guest1.Id,
            RoomId = checkInRoom.Id,
            CheckIn = DateTime.UtcNow.AddDays(-1), // Llegó ayer (CheckIn vs CheckInDate)
            CheckOut = DateTime.UtcNow.AddDays(2), // Se va en 2 días
            Status = ReservationStatus.CheckedIn,
            Adults = 2,
            Children = 0,
            TotalAmount = checkInRoom.BasePrice * 3, // TotalAmount vs TotalPrice
            CreatedAt = DateTimeOffset.UtcNow
        };

        await _context.Reservations.AddAsync(reservation);
        await _context.SaveChangesAsync();

        // 5. Crear Folio para la reserva activa (GuestFolio)
        // IMPORTANTE: Usamos GuestFolio porque Folio es abstracto
        var folio = new GuestFolio
        {
            Id = Guid.NewGuid(),
            ReservationId = reservation.Id,
            Status = FolioStatus.Open,
            // TotalCharges y TotalPayments no existen, son calculados
        };

        // Agregar transacción inicial (Cargo de habitación)
        var transaction = new FolioTransaction
        {
            Id = Guid.NewGuid(),
            FolioId = folio.Id, // FK manual si es necesario, pero EF lo maneja con la colección
            Type = TransactionType.Charge,
            Amount = reservation.TotalAmount,
            Description = ""Cargo por Alojamiento (3 noches)"",
            Quantity = 1,
            UnitPrice = reservation.TotalAmount,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedByUserId = ""SEED-SYSTEM"" // Campo requerido
            // Category eliminado, no existe en tu entidad
        };

        folio.Transactions.Add(transaction);

        await _context.Folios.AddAsync(folio); // EF Core guardará el GuestFolio en la tabla Folios
        await _context.SaveChangesAsync();

        return Ok(new 
        { 
            Message = ""Database Seeded Successfully v2"", 
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
Write-Host "✅ SeedController.cs corregido y alineado con tus Entidades." -ForegroundColor Green
Write-Host "🚀 Ejecuta 'dotnet build' para verificar." -ForegroundColor Cyan