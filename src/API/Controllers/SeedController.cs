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
    private readonly Random _random = new();

    public SeedController(PmsDbContext context)
    {
        _context = context;
    }

    [HttpPost("init")]
    public async Task<IActionResult> InitializeDatabase()
    {
        // 1. LIMPIEZA TOTAL (Orden inverso por FKs)
        _context.Set<FolioTransaction>().RemoveRange(await _context.Set<FolioTransaction>().ToListAsync());
        _context.Folios.RemoveRange(await _context.Folios.ToListAsync());
        _context.Reservations.RemoveRange(await _context.Reservations.ToListAsync());
        _context.CashierShifts.RemoveRange(await _context.CashierShifts.ToListAsync());
        _context.Guests.RemoveRange(await _context.Guests.ToListAsync());
        _context.Rooms.RemoveRange(await _context.Rooms.ToListAsync());
        _context.Products.RemoveRange(await _context.Products.ToListAsync());
        await _context.SaveChangesAsync();

        // 2. PRODUCTOS (Minibar/Servicios)
        var products = new List<Product>
        {
            new() { Id = Guid.NewGuid(), Name = "Coca Cola", UnitPrice = 5000, Category = "Minibar", Stock = 100, IsActive = true, CreatedAt = DateTime.UtcNow },
            new() { Id = Guid.NewGuid(), Name = "Cerveza Club Colombia", UnitPrice = 8000, Category = "Minibar", Stock = 100, IsActive = true, CreatedAt = DateTime.UtcNow },
            new() { Id = Guid.NewGuid(), Name = "Lavandería Express", UnitPrice = 25000, Category = "Servicios", Stock = 999, IsActive = true, CreatedAt = DateTime.UtcNow },
            new() { Id = Guid.NewGuid(), Name = "Desayuno Buffet", UnitPrice = 35000, Category = "Restaurante", Stock = 999, IsActive = true, CreatedAt = DateTime.UtcNow }
        };
        await _context.Products.AddRangeAsync(products);

        // 3. HABITACIONES (30 habitaciones para más volumen)
        var rooms = new List<Room>();
        for (int i = 101; i <= 130; i++)
        {
            var floor = i < 115 ? 1 : 2;
            var type = i % 5 == 0 ? "Suite" : (i % 2 == 0 ? "Doble" : "Sencilla");
            var price = type == "Suite" ? 450000 : (type == "Doble" ? 220000 : 150000);
            
            rooms.Add(new Room
            {
                Id = Guid.NewGuid(),
                Number = i.ToString(),
                Floor = floor,
                Category = type,
                BasePrice = price,
                Status = RoomStatus.Available
            });
        }
        await _context.Rooms.AddRangeAsync(rooms);

        // 4. HUÉSPEDES MASIVOS (200 perfiles)
        var firstNames = new[] { "Juan", "Maria", "Carlos", "Ana", "Pedro", "Luisa", "Jorge", "Sofia", "Diego", "Valentina", "Andres", "Isabella" };
        var lastNames = new[] { "Garcia", "Rodriguez", "Martinez", "Hernandez", "Lopez", "Gonzalez", "Perez", "Sanchez", "Ramirez", "Torres" };
        var nationalities = new[] { "Colombia", "Colombia", "Colombia", "USA", "España", "México", "Argentina", "Francia", "Alemania", "Brasil" };
        
        var guests = new List<Guest>();
        for (int i = 0; i < 200; i++)
        {
            guests.Add(new Guest
            {
                Id = Guid.NewGuid(),
                FirstName = firstNames[_random.Next(firstNames.Length)],
                LastName = lastNames[_random.Next(lastNames.Length)],
                Email = $"user{i}@example.com",
                Phone = $"300{_random.Next(1000000, 9999999)}",
                DocumentNumber = $"{_random.Next(10000000, 99999999)}",
                DocumentType = IdType.CC,
                Nationality = nationalities[_random.Next(nationalities.Length)],
                CreatedAt = DateTime.UtcNow.AddMonths(-3)
            });
        }
        await _context.Guests.AddRangeAsync(guests);
        await _context.SaveChangesAsync();

        // 5. SIMULACIÓN TEMPORAL (Últimos 90 días)
        var startDate = DateTime.UtcNow.Date.AddDays(-90);
        var today = DateTime.UtcNow.Date;
        
        // Iteramos día por día para simular ocupación realista
        for (var date = startDate; date <= today.AddDays(15); date = date.AddDays(1))
        {
            // Determinamos ocupación objetivo del día (Viernes/Sabado más alto)
            bool isWeekend = date.DayOfWeek == DayOfWeek.Friday || date.DayOfWeek == DayOfWeek.Saturday;
            int targetOccupancy = isWeekend ? _random.Next(20, 28) : _random.Next(10, 20); // De 30 habitaciones

            // Intentamos llenar habitaciones disponibles
            var availableRooms = rooms.OrderBy(x => _random.Next()).ToList();
            int roomsBookedToday = 0;

            foreach (var room in availableRooms)
            {
                if (roomsBookedToday >= targetOccupancy) break;

                // Verificamos si la habitación ya está ocupada en esta fecha (lógica simplificada para seeding)
                bool isOccupied = await _context.Reservations.AnyAsync(r => 
                    r.RoomId == room.Id && 
                    r.Status != ReservationStatus.Cancelled &&
                    date >= r.CheckIn && date < r.CheckOut);
                
                if (isOccupied) continue;

                // Crear Reserva
                var nights = isWeekend ? _random.Next(1, 3) : _random.Next(1, 5);
                var checkInDate = date;
                var checkOutDate = date.AddDays(nights);
                
                // Definir estado basado en el tiempo
                ReservationStatus status;
                if (checkOutDate < today) status = ReservationStatus.CheckedOut;
                else if (checkInDate <= today && checkOutDate > today) status = ReservationStatus.CheckedIn;
                else status = ReservationStatus.Confirmed;

                // Si es CheckedIn, actualizar estado real de la habitación
                if (status == ReservationStatus.CheckedIn)
                {
                    var dbRoom = await _context.Rooms.FindAsync(room.Id);
                    if(dbRoom != null) dbRoom.Status = RoomStatus.Occupied;
                }

                var guest = guests[_random.Next(guests.Count)];
                await CreateReservationFlow(room, guest, checkInDate, checkOutDate, status);
                
                roomsBookedToday++;
                
                // Saltamos los días que dura esta reserva para no iterar sobre ellos en el loop externo innecesariamente
                // (Nota: En un seeder simple iteramos por fecha de inicio, así que está bien).
            }
        }
        
        await _context.SaveChangesAsync();
        return Ok(new { Message = "Seeding Masivo Completado: +90 días de historia, ~60% ocupación promedio." });
    }

    private async Task CreateReservationFlow(Room room, Guest guest, DateTime checkIn, DateTime checkOut, ReservationStatus status)
    {
        var total = room.BasePrice * (decimal)(checkOut - checkIn).TotalDays;

        // 1. Reserva
        var reservation = new Reservation
        {
            Id = Guid.NewGuid(),
            ConfirmationCode = $"RES-{_random.Next(10000, 99999)}",
            GuestId = guest.Id,
            RoomId = room.Id,
            CheckIn = checkIn,
            CheckOut = checkOut,
            Status = status,
            Adults = _random.Next(1, 3),
            Children = 0,
            TotalAmount = total,
            CreatedAt = checkIn.AddDays(-_random.Next(1, 10)) // Reservada unos días antes
        };
        await _context.Reservations.AddAsync(reservation);

        // 2. Folio
        var folio = new GuestFolio
        {
            Id = Guid.NewGuid(),
            ReservationId = reservation.Id,
            Status = status == ReservationStatus.CheckedOut ? FolioStatus.Closed : FolioStatus.Open,
            CreatedAt = checkIn
        };
        await _context.Folios.AddAsync(folio);

        // 3. Transacción (Cargo Alojamiento)
        await _context.Set<FolioTransaction>().AddAsync(new FolioTransaction
        {
            Id = Guid.NewGuid(),
            FolioId = folio.Id,
            Type = TransactionType.Charge,
            Amount = total,
            Description = $"Alojamiento {room.Category} x {(checkOut-checkIn).TotalDays} noches",
            CreatedAt = checkIn.AddHours(14) // Checkin time
        });

        // 4. Pago (Si aplica)
        // Si ya salió o está in-house, asumimos pago parcial o total
        if (status == ReservationStatus.CheckedOut || status == ReservationStatus.CheckedIn)
        {
            // A veces pagan todo, a veces dejan saldo si están InHouse
            bool payFull = status == ReservationStatus.CheckedOut || _random.Next(0, 2) == 0; 
            
            if (payFull)
            {
                await _context.Set<FolioTransaction>().AddAsync(new FolioTransaction
                {
                    Id = Guid.NewGuid(),
                    FolioId = folio.Id,
                    Type = TransactionType.Payment,
                    Amount = total,
                    Description = "Pago Saldo Total",
                    PaymentMethod = PaymentMethod.CreditCard,
                    CreatedAt = status == ReservationStatus.CheckedOut ? checkOut.AddHours(10) : checkIn.AddHours(15)
                });
            }
        }
    }
}