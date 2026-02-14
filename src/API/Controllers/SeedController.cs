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
        // Primero borramos transacciones (Hijos)
        await _context.FolioTransactions.ExecuteDeleteAsync();

        // FIX: Borrado manual de jerarquía TPT (Hijos primero, luego Padre)
        await _context.Database.ExecuteSqlRawAsync("DELETE FROM GuestFolios"); 
        await _context.Database.ExecuteSqlRawAsync("DELETE FROM ExternalFolios");
        await _context.Database.ExecuteSqlRawAsync("DELETE FROM Folios");

        // El resto de entidades simples
        await _context.ReservationSegments.ExecuteDeleteAsync();
        await _context.Reservations.ExecuteDeleteAsync();
        await _context.Products.ExecuteDeleteAsync();
        await _context.Rooms.ExecuteDeleteAsync();
        await _context.Guests.ExecuteDeleteAsync();
        
        // Limpieza de caja
        await _context.CashierShifts.ExecuteDeleteAsync();

        // 2. Crear Datos Maestros
        
        // CREAR TURNO SISTEMA (Importante para que las transacciones históricas tengan FK válida)
        var systemShift = new CashierShift
        {
            Id = Guid.NewGuid(),
            UserId = "system",
            OpenedAt = DateTime.UtcNow.AddYears(-1),
            ClosedAt = DateTime.UtcNow.AddYears(-1).AddHours(8),
            StartingAmount = 0, 
            Status = CashierShiftStatus.Closed
        };
        await _context.CashierShifts.AddAsync(systemShift);
        await _context.SaveChangesAsync();

        var rooms = await CreateRooms();
        var guests = await CreateGuests();
        var products = await CreateProducts();

        // 3. Generar Escenarios
        await CreateHistoricalReservations(rooms, guests, products, systemShift.Id);
        await CreateActiveReservations(rooms, guests, products, systemShift.Id);
        await CreateFutureReservations(rooms, guests);
        await CreateSplitStayReservation(rooms, guests[0]);

        return Ok(new { message = "Base de datos reiniciada y poblada correctamente." });
    }

    // --- MÉTODOS DE GENERACIÓN ---

    private async Task<List<Room>> CreateRooms()
    {
        var rooms = new List<Room>();
        var categories = new[] { 
            "Doble", 
            "Triple", 
            "Familiar", 
            "Suite Familiar", 
            "Suite" 
        }; 
        var prices = new[] { 
            130000m, // Doble
            180000m, // Triple
            220000m, // Familiar
            300000m, // Suite Familiar
            340000m  // Suite (La más costosa)
        };

        for (int floor = 1; floor <= 3; floor++)
        {
            for (int num = 1; num <= 10; num++)
            {
                var typeIndex = _random.Next(categories.Length);
                var room = new Room
                {
                    Id = Guid.NewGuid(),
                    Number = $"{floor}{num:00}",
                    Floor = floor,
                    Category = categories[typeIndex],
                    BasePrice = prices[typeIndex],
                    Status = RoomStatus.Available 
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
        var names = new[] { "Juan", "Maria", "Carlos", "Ana", "Pedro", "Sofia", "Luis", "Elena" };
        var lastNames = new[] { "Perez", "Gomez", "Rodriguez", "Lopez", "Martinez", "Sanchez" };

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
            new() { Id = Guid.NewGuid(), Name = "Coca-Cola", Description = "Lata 330ml", UnitPrice = 6000, Category = "Bebidas", Stock = 100, IsActive = true, CreatedAt = DateTime.UtcNow },
            new() { Id = Guid.NewGuid(), Name = "Agua Mineral", Description = "Botella 500ml", UnitPrice = 4000, Category = "Bebidas", Stock = 100, IsActive = true, CreatedAt = DateTime.UtcNow },
            new() { Id = Guid.NewGuid(), Name = "Cerveza Club", Description = "Botella 330ml Ambar", UnitPrice = 8000, Category = "Bebidas", Stock = 100, IsActive = true, CreatedAt = DateTime.UtcNow },
            new() { Id = Guid.NewGuid(), Name = "Hamburguesa Clásica", Description = "150g Res, Queso, Papas", UnitPrice = 32000, Category = "Platos", Stock = 20, IsActive = true, CreatedAt = DateTime.UtcNow },
            new() { Id = Guid.NewGuid(), Name = "Club Sandwich", Description = "Pollo, tocineta, huevo", UnitPrice = 28000, Category = "Platos", Stock = 15, IsActive = true, CreatedAt = DateTime.UtcNow },
            new() { Id = Guid.NewGuid(), Name = "Lavandería Express", Description = "Lavado y planchado rápido", UnitPrice = 25000, Category = "Servicios", Stock = 999, IsActive = true, CreatedAt = DateTime.UtcNow }
        };
        await _context.Products.AddRangeAsync(products);
        await _context.SaveChangesAsync();
        return products;
    }

    private async Task CreateHistoricalReservations(List<Room> rooms, List<Guest> guests, List<Product> products, Guid shiftId)
    {
        for (int i = 0; i < 30; i++)
        {
            var room = rooms[_random.Next(rooms.Count)];
            var guest = guests[_random.Next(guests.Count)];
            var checkIn = DateTime.UtcNow.Date.AddDays(-_random.Next(10, 60));
            var checkOut = checkIn.AddDays(_random.Next(1, 4));

            await CreateFullReservationFlow(room, guest, checkIn, checkOut, ReservationStatus.CheckedOut, products, true, shiftId);
        }
    }

    private async Task CreateActiveReservations(List<Room> rooms, List<Guest> guests, List<Product> products, Guid shiftId)
    {
        var activeRooms = rooms.Take(5).ToList();
        foreach (var room in activeRooms)
        {
            var guest = guests[_random.Next(guests.Count)];
            var checkIn = DateTime.UtcNow.Date.AddDays(-1);
            var checkOut = checkIn.AddDays(3);

            await CreateFullReservationFlow(room, guest, checkIn, checkOut, ReservationStatus.CheckedIn, products, false, shiftId);
            
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
                Adults = _random.Next(1, 3),
                Children = _random.Next(0, 2),
                TotalAmount = room.BasePrice * 2,
                CreatedAt = DateTimeOffset.UtcNow
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
        }
        await _context.SaveChangesAsync();
    }

    private async Task CreateSplitStayReservation(List<Room> rooms, Guest guest)
    {
        // Buscamos habitaciones por índice para asegurar que existan
        var room1 = rooms[0];
        var room2 = rooms[1];
        
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
            Adults = 2,
            Children = 0,
            TotalAmount = (room1.BasePrice * 2) + room2.BasePrice,
            Notes = "Split Stay Demo - Cambio de habitación",
            CreatedAt = DateTimeOffset.UtcNow
        };

        res.Segments.Add(new ReservationSegment { Id=Guid.NewGuid(), RoomId = room1.Id, CheckIn = checkIn, CheckOut = moveDate });
        res.Segments.Add(new ReservationSegment { Id=Guid.NewGuid(), RoomId = room2.Id, CheckIn = moveDate, CheckOut = checkOut });

        await _context.Reservations.AddAsync(res);
        await _context.SaveChangesAsync();
    }

    private async Task CreateFullReservationFlow(Room room, Guest guest, DateTime checkIn, DateTime checkOut, ReservationStatus status, List<Product> products, bool isFinished, Guid shiftId)
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
            Adults = _random.Next(1, 3),
            Children = _random.Next(0, 2),
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
            Id = Guid.NewGuid(),
            FolioId = folio.Id,
            Amount = total,
            Description = "Hospedaje",
            Type = TransactionType.Charge,
            CashierShiftId = shiftId, // IMPORTANTE: Asignamos el turno dummy
            CreatedAt = checkIn
        });

        if (_random.NextDouble() > 0.5)
        {
            var p = products[_random.Next(products.Count)];
            await _context.FolioTransactions.AddAsync(new FolioTransaction
            {
                Id = Guid.NewGuid(),
                FolioId = folio.Id,
                Amount = p.UnitPrice,
                Description = $"Consumo: {p.Name}",
                Type = TransactionType.Charge,
                Quantity = 1,
                UnitPrice = p.UnitPrice,
                CashierShiftId = shiftId,
                CreatedAt = checkIn.AddHours(1)
            });
            // Al hacer "TotalAmount += p.UnitPrice" actualizamos lo que debe el cliente
            res.TotalAmount += p.UnitPrice;
        }

        if (isFinished)
        {
            await _context.FolioTransactions.AddAsync(new FolioTransaction
            {
                Id = Guid.NewGuid(),
                FolioId = folio.Id,
                Amount = res.TotalAmount, 
                Description = "Pago Total",
                Type = TransactionType.Payment,
                PaymentMethod = PaymentMethod.CreditCard,
                CashierShiftId = shiftId,
                CreatedAt = checkOut
            });
        }

        await _context.SaveChangesAsync();
    }
}