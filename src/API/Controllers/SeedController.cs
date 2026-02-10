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
        // ==========================================
        // 1. LIMPIEZA DE DATOS (Orden por FKs)
        // ==========================================
        
        // Productos
        var products = await _context.Products.ToListAsync();
        _context.Products.RemoveRange(products);

        // Transacciones y Folios
        var transactions = await _context.Set<FolioTransaction>().ToListAsync();
        _context.RemoveRange(transactions);
        
        var folios = await _context.Folios.ToListAsync();
        _context.Folios.RemoveRange(folios);

        // Reservas
        var reservations = await _context.Reservations.ToListAsync();
        _context.Reservations.RemoveRange(reservations);

        // Huéspedes
        var guests = await _context.Guests.ToListAsync();
        _context.Guests.RemoveRange(guests);

        // Habitaciones
        var rooms = await _context.Rooms.ToListAsync();
        _context.Rooms.RemoveRange(rooms);
        
        await _context.SaveChangesAsync();

        // ==========================================
        // 2. CREAR PRODUCTOS (Inventario)
        // ==========================================
        var productsList = new List<Product>
        {
            // Bebidas
            new Product { Id = Guid.NewGuid(), Name = "Coca-Cola 350ml", Category = "Bebidas", UnitPrice = 4500, Stock = 48, Description = "Lata refrescante bien fria", ImageUrl = "https://images.unsplash.com/photo-1622483767028-3f66f32aef97?auto=format&fit=crop&w=400&q=80" },
            new Product { Id = Guid.NewGuid(), Name = "Agua Manantial 600ml", Category = "Bebidas", UnitPrice = 3500, Stock = 100, Description = "Sin gas", ImageUrl = "https://images.unsplash.com/photo-1560733612-4d95d03837cd?auto=format&fit=crop&w=400&q=80" },
            new Product { Id = Guid.NewGuid(), Name = "Cerveza Corona", Category = "Bebidas", UnitPrice = 9000, Stock = 24, Description = "Importada", ImageUrl = "https://images.unsplash.com/photo-1622483767128-4f738a9e227e?auto=format&fit=crop&w=400&q=80" }, // Placeholder genérico
            
            // Snacks
            new Product { Id = Guid.NewGuid(), Name = "Papas Margarita Pollo", Category = "Snacks", UnitPrice = 4500, Stock = 15, Description = "Paquete familiar", ImageUrl = "https://images.unsplash.com/photo-1566478989037-eec170784d0b?auto=format&fit=crop&w=400&q=80" },
            new Product { Id = Guid.NewGuid(), Name = "Maní con Sal", Category = "Snacks", UnitPrice = 3000, Stock = 30, Description = "La Especial", ImageUrl = "https://images.unsplash.com/photo-1632517594943-4b68425a0753?auto=format&fit=crop&w=400&q=80" },
            
            // Licores
            new Product { Id = Guid.NewGuid(), Name = "Whisky Buchanan's 12 (Media)", Category = "Licores", UnitPrice = 120000, Stock = 5, Description = "Botella 375ml", ImageUrl = "https://images.unsplash.com/photo-1527281400683-1aae777175f8?auto=format&fit=crop&w=400&q=80" },
            new Product { Id = Guid.NewGuid(), Name = "Ron Medellín Añejo", Category = "Licores", UnitPrice = 65000, Stock = 8, Description = "Botella 750ml", ImageUrl = "https://images.unsplash.com/photo-1614313511387-1436a4480ebb?auto=format&fit=crop&w=400&q=80" },

            // Amenities / Servicios
            new Product { Id = Guid.NewGuid(), Name = "Kit de Aseo Premium", Category = "Amenities", UnitPrice = 15000, Stock = 50, Description = "Cepillo, crema, hilo dental", ImageUrl = "https://images.unsplash.com/photo-1631729371254-42c2892f0e6e?auto=format&fit=crop&w=400&q=80" },
            new Product { Id = Guid.NewGuid(), Name = "Toalla Piscina (Venta)", Category = "Amenities", UnitPrice = 45000, Stock = 10, Description = "Toalla de lujo bordada", ImageUrl = "https://images.unsplash.com/photo-1616627781431-23b776a541b0?auto=format&fit=crop&w=400&q=80" },
            new Product { Id = Guid.NewGuid(), Name = "Servicio Lavandería (Bolsa)", Category = "Servicios", UnitPrice = 25000, Stock = 999, Description = "Lavado y secado estándar", ImageUrl = "https://images.unsplash.com/photo-1545173168-9f1947eebb8f?auto=format&fit=crop&w=400&q=80" }
        };

        await _context.Products.AddRangeAsync(productsList);
        await _context.SaveChangesAsync();

        // ==========================================
        // 3. CREAR HABITACIONES (10)
        // ==========================================
        var roomsList = new List<Room>();
        for (int i = 1; i <= 10; i++)
        {
            string category;
            decimal price;

            if (i <= 3) { category = "Doble"; price = 120000m; }
            else if (i <= 6) { category = "Triple"; price = 180000m; }
            else if (i <= 8) { category = "Familiar"; price = 250000m; }
            else { category = "SuiteFamiliar"; price = 350000m; }

            roomsList.Add(new Room
            {
                Id = Guid.NewGuid(),
                Number = $"10{i-1}",
                Category = category,
                BasePrice = price,
                Status = RoomStatus.Available, // Se actualizará abajo si hay reserva activa
                Floor = 1
            });
        }
        await _context.Rooms.AddRangeAsync(roomsList);
        await _context.SaveChangesAsync();

        // ==========================================
        // 4. CREAR HUÉSPEDES (5)
        // ==========================================
        var guestsList = new List<Guest>
        {
            new Guest { Id = Guid.NewGuid(), FirstName = "Juan", LastName = "Pérez", Email = "juan@mail.com", Phone = "3001234567", DocumentNumber = "1098765432", DocumentType = IdType.CC, Nationality = "Colombia", CreatedAt = DateTime.UtcNow },
            new Guest { Id = Guid.NewGuid(), FirstName = "Maria", LastName = "Gómez", Email = "maria@mail.com", Phone = "3109876543", DocumentNumber = "987654321", DocumentType = IdType.CC, Nationality = "Colombia", CreatedAt = DateTime.UtcNow },
            new Guest { Id = Guid.NewGuid(), FirstName = "Carlos", LastName = "Rodríguez", Email = "carlos@mail.com", Phone = "3201112233", DocumentNumber = "876543210", DocumentType = IdType.CE, Nationality = "México", CreatedAt = DateTime.UtcNow },
            new Guest { Id = Guid.NewGuid(), FirstName = "Laura", LastName = "Martínez", Email = "laura@mail.com", Phone = "3155556677", DocumentNumber = "1122334455", DocumentType = IdType.CC, Nationality = "Colombia", CreatedAt = DateTime.UtcNow },
            new Guest { Id = Guid.NewGuid(), FirstName = "John", LastName = "Smith", Email = "john@usa.com", Phone = "+15550001", DocumentNumber = "A12345678", DocumentType = IdType.PA, Nationality = "USA", CreatedAt = DateTime.UtcNow }
        };
        await _context.Guests.AddRangeAsync(guestsList);
        await _context.SaveChangesAsync();

        // ==========================================
        // 5. CREAR RESERVAS Y FOLIOS
        // ==========================================
        
        // Reserva 1: ACTIVA (Check-in ayer, sale mañana) - Hab 100
        await CreateReservation(
            _context, 
            roomsList[0], 
            guestsList[0], 
            DateTime.UtcNow.AddDays(-1), 
            DateTime.UtcNow.AddDays(2), 
            ReservationStatus.CheckedIn, 
            RoomStatus.Occupied,
            2);

        // Reserva 2: ACTIVA (Check-in hoy, sale en 3 días) - Hab 101
        await CreateReservation(
            _context, 
            roomsList[1], 
            guestsList[2], 
            DateTime.UtcNow, 
            DateTime.UtcNow.AddDays(3), 
            ReservationStatus.CheckedIn, 
            RoomStatus.Occupied,
            1);

        // Reserva 3: FUTURA (Confirmada, llega la otra semana) - Hab 102
        await CreateReservation(
            _context, 
            roomsList[2], 
            guestsList[1], 
            DateTime.UtcNow.AddDays(5), 
            DateTime.UtcNow.AddDays(8), 
            ReservationStatus.Confirmed, 
            RoomStatus.Available, // Aún no llega
            3);

        // Reserva 4: PASADA (Check-out hace 2 días) - Hab 103
        await CreateReservation(
            _context, 
            roomsList[3], 
            guestsList[3], 
            DateTime.UtcNow.AddDays(-5), 
            DateTime.UtcNow.AddDays(-2), 
            ReservationStatus.CheckedOut, 
            RoomStatus.TouchUp, // Quedó sucia
            2);

        return Ok(new 
        { 
            Message = "Base de datos poblada con éxito (v4)", 
            Stats = new { 
                Rooms = roomsList.Count, 
                Guests = guestsList.Count, 
                Products = productsList.Count,
                Reservations = 4 
            } 
        });
    }

    // Helper local para crear reserva + folio + transacción inicial
    private async Task CreateReservation(
        PmsDbContext context, 
        Room room, 
        Guest guest, 
        DateTime checkIn, 
        DateTime checkOut, 
        ReservationStatus resStatus, 
        RoomStatus roomStatus,
        int adults)
    {
        // Actualizar estado habitación
        room.Status = roomStatus;
        context.Rooms.Update(room);

        // Calcular noches y total
        var nights = (checkOut - checkIn).Days;
        if (nights < 1) nights = 1;
        var totalAmount = room.BasePrice * nights;

        // Crear Reserva
        var reservation = new Reservation
        {
            Id = Guid.NewGuid(),
            ConfirmationCode = $"RES-{new Random().Next(10000, 99999)}",
            GuestId = guest.Id,
            RoomId = room.Id,
            CheckIn = checkIn,
            CheckOut = checkOut,
            Status = resStatus,
            Adults = adults,
            Children = 0,
            TotalAmount = totalAmount,
            CreatedAt = DateTime.UtcNow,
            Notes = "Generado por Seed"
        };
        await context.Reservations.AddAsync(reservation);

        // Crear Folio
        var folio = new GuestFolio
        {
            Id = Guid.NewGuid(),
            ReservationId = reservation.Id,
            Status = resStatus == ReservationStatus.CheckedOut ? FolioStatus.Closed : FolioStatus.Open,
            CreatedAt = DateTime.UtcNow
        };
        await context.Folios.AddAsync(folio);

        // Crear Cargo por Habitación (Transaction)
        var transaction = new FolioTransaction
        {
            Id = Guid.NewGuid(),
            FolioId = folio.Id,
            Type = TransactionType.Charge,
            Amount = totalAmount,
            Description = $"Alojamiento x {nights} Noches - {room.Category}",
            Quantity = 1,
            UnitPrice = totalAmount,
            CreatedAt = DateTime.UtcNow,
            CreatedByUserId = "SEED-SYSTEM"
        };
        await context.Set<FolioTransaction>().AddAsync(transaction);

        await context.SaveChangesAsync();
    }
}