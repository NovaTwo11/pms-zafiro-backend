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
        // 1. LIMPIEZA
        _context.Products.RemoveRange(await _context.Products.ToListAsync());
        _context.Set<FolioTransaction>().RemoveRange(await _context.Set<FolioTransaction>().ToListAsync());
        _context.Folios.RemoveRange(await _context.Folios.ToListAsync());
        _context.Reservations.RemoveRange(await _context.Reservations.ToListAsync());
        _context.Guests.RemoveRange(await _context.Guests.ToListAsync());
        _context.Rooms.RemoveRange(await _context.Rooms.ToListAsync());
        await _context.SaveChangesAsync();

        // 2. HABITACIONES (20 habitaciones)
        var rooms = new List<Room>();
        for (int i = 101; i <= 120; i++)
        {
            var floor = i < 110 ? 1 : 2;
            var type = i % 3 == 0 ? "Suite" : (i % 2 == 0 ? "Doble" : "Sencilla");
            var price = type == "Suite" ? 350000 : (type == "Doble" ? 200000 : 120000);
            
            rooms.Add(new Room
            {
                Id = Guid.NewGuid(),
                Number = i.ToString(),
                Floor = floor,
                Category = type,
                BasePrice = price,
                Status = RoomStatus.Available // Se actualizará al crear reservas
            });
        }
        await _context.Rooms.AddRangeAsync(rooms);

        // 3. HUÉSPEDES (50 perfiles variados para demografía)
        var nationalities = new[] { "Colombia", "Colombia", "Colombia", "USA", "España", "México", "Argentina", "Francia" };
        var guests = new List<Guest>();
        for (int i = 0; i < 50; i++)
        {
            guests.Add(new Guest
            {
                Id = Guid.NewGuid(),
                FirstName = $"Guest{i}",
                LastName = $"Test{i}",
                Email = $"guest{i}@zafiro.com",
                Phone = $"300000{i:0000}",
                DocumentNumber = $"DOC{i}",
                DocumentType = IdType.CC,
                Nationality = nationalities[_random.Next(nationalities.Length)],
                CreatedAt = DateTime.UtcNow
            });
        }
        await _context.Guests.AddRangeAsync(guests);
        await _context.SaveChangesAsync();

        // 4. GENERAR HISTORIAL (Últimos 30 días)
        var pastDate = DateTime.UtcNow.AddDays(-30);
        var today = DateTime.UtcNow;

        // Generar 40 reservas pasadas (Check-out ya realizado)
        for (int i = 0; i < 40; i++)
        {
            var checkIn = pastDate.AddDays(_random.Next(0, 25));
            var nights = _random.Next(1, 4);
            var checkOut = checkIn.AddDays(nights);
            
            // Elegir habitación y huésped al azar
            var room = rooms[_random.Next(rooms.Count)];
            var guest = guests[_random.Next(guests.Count)];

            await CreateReservationFlow(room, guest, checkIn, checkOut, ReservationStatus.CheckedOut, isPast: true);
        }

        // 5. GENERAR ACTUALIDAD (Check-ins de hoy y "InHouse")
        // 5 Reservas activas (InHouse)
        for (int i = 0; i < 5; i++)
        {
            var room = rooms[i]; // Ocupamos las primeras
            var guest = guests[i];
            var checkIn = today.AddDays(-_random.Next(1, 3));
            var checkOut = today.AddDays(_random.Next(1, 3));
            
            await CreateReservationFlow(room, guest, checkIn, checkOut, ReservationStatus.CheckedIn, isPast: false);
        }

        // 3 Llegadas para HOY (Pendientes)
        for (int i = 5; i < 8; i++)
        {
            var room = rooms[i];
            var guest = guests[i];
            // CheckIn es HOY, CheckOut en el futuro
            await CreateReservationFlow(room, guest, today, today.AddDays(2), ReservationStatus.Confirmed, isPast: false);
        }

        await _context.SaveChangesAsync();
        return Ok(new { Message = "Base de datos poblada con Historial y Actividad Reciente" });
    }

    private async Task CreateReservationFlow(Room room, Guest guest, DateTime checkIn, DateTime checkOut, ReservationStatus status, bool isPast)
    {
        var total = room.BasePrice * (decimal)(checkOut - checkIn).TotalDays;

        var reservation = new Reservation
        {
            Id = Guid.NewGuid(),
            ConfirmationCode = $"RES-{_random.Next(10000, 99999)}",
            GuestId = guest.Id,
            RoomId = room.Id,
            CheckIn = checkIn,
            CheckOut = checkOut,
            Status = status,
            Adults = 2,
            TotalAmount = total,
            CreatedAt = checkIn // Importante para "Actividad Reciente"
        };

        // Si está activa, marcamos la habitación
        if (status == ReservationStatus.CheckedIn)
        {
            room.Status = RoomStatus.Occupied;
            _context.Rooms.Update(room);
        }

        await _context.Reservations.AddAsync(reservation);

        // Crear Folio
        var folio = new GuestFolio
        {
            Id = Guid.NewGuid(),
            ReservationId = reservation.Id,
            Status = status == ReservationStatus.CheckedOut ? FolioStatus.Closed : FolioStatus.Open,
            CreatedAt = checkIn
        };
        await _context.Folios.AddAsync(folio);

        // Transacción de Cargo (Alojamiento)
        await _context.Set<FolioTransaction>().AddAsync(new FolioTransaction
        {
            Id = Guid.NewGuid(),
            FolioId = folio.Id,
            Type = TransactionType.Charge,
            Amount = total,
            Description = "Cargo Alojamiento",
            CreatedAt = checkIn
        });

        // Si es pasada, simulamos el PAGO para que salga en la gráfica de Ingresos
        if (isPast || status == ReservationStatus.CheckedOut)
        {
            await _context.Set<FolioTransaction>().AddAsync(new FolioTransaction
            {
                Id = Guid.NewGuid(),
                FolioId = folio.Id,
                Type = TransactionType.Payment,
                Amount = total,
                Description = "Pago Efectivo/Tarjeta",
                PaymentMethod = PaymentMethod.CreditCard,
                // Fecha de pago = Fecha de salida
                CreatedAt = checkOut 
            });
        }
    }
}