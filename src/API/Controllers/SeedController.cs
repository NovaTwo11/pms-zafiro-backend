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
    private readonly Random _random = new Random(12345); 

    public SeedController(PmsDbContext context)
    {
        _context = context;
    }

    [HttpPost("init")]
    public async Task<IActionResult> Initialize()
    {
        // 1. Limpieza de Base de Datos
        // Usamos ExecuteSqlRawAsync para las tablas con herencia (TPT) donde ExecuteDelete falla.
        
        // Primero borramos transacciones (Hijos)
        await _context.FolioTransactions.ExecuteDeleteAsync();

        // FIX: Borrado manual de jerarquía TPT (Hijos primero, luego Padre)
        // Nota: Si tus tablas tienen nombres diferentes en BD, ajústalos aquí. 
        // Basado en tu DbContext: GuestFolios, ExternalFolios, Folios.
        await _context.Database.ExecuteSqlRawAsync("DELETE FROM GuestFolios"); 
        await _context.Database.ExecuteSqlRawAsync("DELETE FROM ExternalFolios");
        await _context.Database.ExecuteSqlRawAsync("DELETE FROM Folios");

        // El resto de entidades simples sí soportan ExecuteDeleteAsync
        await _context.ReservationSegments.ExecuteDeleteAsync();
        await _context.Reservations.ExecuteDeleteAsync();
        await _context.Products.ExecuteDeleteAsync();
        await _context.Rooms.ExecuteDeleteAsync();
        await _context.Guests.ExecuteDeleteAsync();
        
        // Limpieza de caja (si aplica)
        await _context.CashierShifts.ExecuteDeleteAsync();

        // 2. Crear Datos Maestros
        var rooms = await CreateRooms();
        var guests = await CreateGuests();
        var products = await CreateProducts();

        // 3. Generar Escenarios
        await CreateHistoricalReservations(rooms, guests, products);
        await CreateActiveReservations(rooms, guests, products);
        await CreateFutureReservations(rooms, guests);
        await CreateSplitStayReservation(rooms, guests[0]);

        return Ok(new { message = "Base de datos reiniciada y poblada correctamente." });
    }

    // --- MÉTODOS DE GENERACIÓN (Sin cambios lógicos, solo asegurando nombres correctos) ---

    private async Task<List<Room>> CreateRooms()
    {
        var rooms = new List<Room>();
        var types = new[] { "Sencilla", "Doble", "Suite", "Familiar" };
        var prices = new[] { 150000m, 220000m, 350000m, 280000m };

        for (int floor = 1; floor <= 3; floor++)
        {
            for (int num = 1; num <= 10; num++)
            {
                var typeIndex = _random.Next(types.Length);
                var room = new Room
                {
                    Id = Guid.NewGuid(),
                    Number = $"{floor}{num:00}",
                    Floor = floor,
                    Category = types[typeIndex],
                    BasePrice = prices[typeIndex],
                    Status = RoomStatus.Available // Asegúrate que tu Enum tenga Clean o Available
                };
                rooms.Add(room);
            }
        }
        await _context.Rooms.AddRangeAsync(rooms);
        await _context.SaveChangesAsync();
        return rooms;
    }

    private async Task<List<Guest>> CreateGuests()
    {
        var guests = new List<Guest>();
        var names = new[] { "Juan", "Maria", "Carlos", "Ana", "Pedro", "Sofia" };
        var lastNames = new[] { "Perez", "Gomez", "Rodriguez", "Lopez" };

        for (int i = 0; i < 50; i++)
        {
            guests.Add(new Guest
            {
                Id = Guid.NewGuid(),
                FirstName = names[_random.Next(names.Length)],
                LastName = lastNames[_random.Next(lastNames.Length)],
                DocumentType = IdType.CC,
                DocumentNumber = _random.Next(10000000, 99999999).ToString(),
                Email = $"guest{i}@test.com",
                Phone = "3001234567",
                Nationality = "Colombia",
                CreatedAt = DateTimeOffset.UtcNow.AddMonths(-6)
            });
        }
        await _context.Guests.AddRangeAsync(guests);
        await _context.SaveChangesAsync();
        return guests;
    }

    private async Task<List<Product>> CreateProducts()
    {
        var products = new List<Product>
        {
            // Ojo: Usamos UnitPrice porque así está en tu entidad Product
            new() { Name = "Coca Cola", UnitPrice = 5000, Category = "Minibar", Stock = 100 },
            new() { Name = "Agua", UnitPrice = 4000, Category = "Minibar", Stock = 100 },
            new() { Name = "Lavandería", UnitPrice = 25000, Category = "Servicios", Stock = 999 }
        };
        await _context.Products.AddRangeAsync(products);
        await _context.SaveChangesAsync();
        return products;
    }

    private async Task CreateHistoricalReservations(List<Room> rooms, List<Guest> guests, List<Product> products)
    {
        for (int i = 0; i < 30; i++)
        {
            var room = rooms[_random.Next(rooms.Count)];
            var guest = guests[_random.Next(guests.Count)];
            var checkIn = DateTime.UtcNow.Date.AddDays(-_random.Next(10, 60));
            var checkOut = checkIn.AddDays(_random.Next(1, 4));

            await CreateFullReservationFlow(room, guest, checkIn, checkOut, ReservationStatus.CheckedOut, products, true);
        }
    }

    private async Task CreateActiveReservations(List<Room> rooms, List<Guest> guests, List<Product> products)
    {
        var activeRooms = rooms.Take(5).ToList();
        foreach (var room in activeRooms)
        {
            var guest = guests[_random.Next(guests.Count)];
            var checkIn = DateTime.UtcNow.Date.AddDays(-1);
            var checkOut = checkIn.AddDays(3);

            await CreateFullReservationFlow(room, guest, checkIn, checkOut, ReservationStatus.CheckedIn, products, false);
            
            room.Status = RoomStatus.Occupied;
            _context.Rooms.Update(room);
        }
        await _context.SaveChangesAsync();
    }

    private async Task CreateFutureReservations(List<Room> rooms, List<Guest> guests)
    {
        for (int i = 0; i < 10; i++)
        {
            var room = rooms[_random.Next(rooms.Count)];
            var guest = guests[_random.Next(guests.Count)];
            var checkIn = DateTime.UtcNow.Date.AddDays(_random.Next(5, 20));
            var checkOut = checkIn.AddDays(2);

            var res = new Reservation
            {
                Id = Guid.NewGuid(),
                ConfirmationCode = Guid.NewGuid().ToString().Substring(0, 6).ToUpper(),
                GuestId = guest.Id,
                CheckIn = checkIn,
                CheckOut = checkOut,
                Status = ReservationStatus.Confirmed,
                TotalAmount = room.BasePrice * 2,
                CreatedAt = DateTimeOffset.UtcNow
            };
            
            // Segmento único
            res.Segments.Add(new ReservationSegment
            {
                Id = Guid.NewGuid(),
                ReservationId = res.Id,
                RoomId = room.Id,
                CheckIn = checkIn,
                CheckOut = checkOut
            });

            await _context.Reservations.AddAsync(res);
        }
        await _context.SaveChangesAsync();
    }

    private async Task CreateSplitStayReservation(List<Room> rooms, Guest guest)
    {
        var room1 = rooms.First(r => r.Number == "101");
        var room2 = rooms.First(r => r.Number == "102");
        
        var checkIn = DateTime.UtcNow.Date.AddDays(2);
        var moveDate = checkIn.AddDays(2);
        var checkOut = moveDate.AddDays(1);

        var res = new Reservation
        {
            Id = Guid.NewGuid(),
            ConfirmationCode = "SPLIT-01",
            GuestId = guest.Id,
            CheckIn = checkIn,
            CheckOut = checkOut,
            Status = ReservationStatus.Confirmed,
            TotalAmount = (room1.BasePrice * 2) + room2.BasePrice,
            Notes = "Split Stay Demo",
            CreatedAt = DateTimeOffset.UtcNow
        };

        res.Segments.Add(new ReservationSegment { RoomId = room1.Id, CheckIn = checkIn, CheckOut = moveDate });
        res.Segments.Add(new ReservationSegment { RoomId = room2.Id, CheckIn = moveDate, CheckOut = checkOut });

        await _context.Reservations.AddAsync(res);
        await _context.SaveChangesAsync();
    }

    private async Task CreateFullReservationFlow(Room room, Guest guest, DateTime checkIn, DateTime checkOut, ReservationStatus status, List<Product> products, bool isFinished)
    {
        var nights = (checkOut - checkIn).Days;
        var total = room.BasePrice * nights;

        var res = new Reservation
        {
            Id = Guid.NewGuid(),
            ConfirmationCode = Guid.NewGuid().ToString().Substring(0, 6).ToUpper(),
            GuestId = guest.Id,
            CheckIn = checkIn,
            CheckOut = checkOut,
            Status = status,
            TotalAmount = total,
            CreatedAt = checkIn.AddDays(-5)
        };

        res.Segments.Add(new ReservationSegment
        {
            Id = Guid.NewGuid(),
            ReservationId = res.Id,
            RoomId = room.Id,
            CheckIn = checkIn,
            CheckOut = checkOut
        });

        await _context.Reservations.AddAsync(res);

        var folio = new GuestFolio
        {
            Id = Guid.NewGuid(),
            ReservationId = res.Id,
            Status = isFinished ? FolioStatus.Closed : FolioStatus.Open,
            CreatedAt = checkIn
        };
        await _context.Folios.AddAsync(folio);

        await _context.FolioTransactions.AddAsync(new FolioTransaction
        {
            FolioId = folio.Id,
            Amount = total,
            Description = "Hospedaje",
            Type = TransactionType.Charge,
            CreatedAt = checkIn
        });

        if (_random.NextDouble() > 0.5)
        {
            var p = products[0];
            await _context.FolioTransactions.AddAsync(new FolioTransaction
            {
                FolioId = folio.Id,
                Amount = p.UnitPrice,
                Description = $"Consumo {p.Name}",
                Type = TransactionType.Charge,
                Quantity = 1,
                UnitPrice = p.UnitPrice,
                CreatedAt = checkIn.AddHours(1)
            });
        }

        if (isFinished)
        {
            await _context.FolioTransactions.AddAsync(new FolioTransaction
            {
                FolioId = folio.Id,
                Amount = res.TotalAmount,
                Description = "Pago Total",
                Type = TransactionType.Payment,
                PaymentMethod = PaymentMethod.CreditCard,
                CreatedAt = checkOut
            });
        }

        await _context.SaveChangesAsync();
    }
}