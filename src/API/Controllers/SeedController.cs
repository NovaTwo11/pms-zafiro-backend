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
    public async Task<IActionResult> InitializeForProduction()
    {
        try 
        {
            // =========================================================
            // 1. LIMPIEZA PROFUNDA DE BASE DE DATOS (PRODUCCIÓN)
            // =========================================================
            await _context.FolioTransactions.ExecuteDeleteAsync();

            // Borrado de tablas con herencia en PostgreSQL
            await _context.Database.ExecuteSqlRawAsync("DELETE FROM \"GuestFolios\""); 
            await _context.Database.ExecuteSqlRawAsync("DELETE FROM \"ExternalFolios\"");
            await _context.Database.ExecuteSqlRawAsync("DELETE FROM \"Folios\"");

            await _context.ReservationSegments.ExecuteDeleteAsync();
            await _context.ReservationGuests.ExecuteDeleteAsync(); 
            await _context.Reservations.ExecuteDeleteAsync();
            await _context.Products.ExecuteDeleteAsync();
            await _context.Rooms.ExecuteDeleteAsync();
            await _context.Guests.ExecuteDeleteAsync();
            await _context.CashierShifts.ExecuteDeleteAsync();
            
            // Limpiar usuarios
            await _context.Database.ExecuteSqlRawAsync("DELETE FROM \"Users\""); 
            
            // =========================================================
            // 2. CREACIÓN DE USUARIO ADMINISTRADOR
            // =========================================================
            var adminUser = new User
            {
                Username = "WilmerAdmin",
                PasswordHash = "Zafiro2025!", // Asegúrate de que tu login procese la contraseña en crudo o hasheada según lo tengas configurado
                Role = "Admin",
                CreatedAt = DateTime.UtcNow
            };
            await _context.Users.AddAsync(adminUser);
            await _context.SaveChangesAsync(); 

            // =========================================================
            // 3. CREACIÓN DE HABITACIONES EXACTAS
            // =========================================================
            await CreateProductionRooms();

            // =========================================================
            // 4. CREACIÓN DEL CATÁLOGO DE PRODUCTOS (SEGÚN MENÚ)
            // =========================================================
            await CreateProductionProducts();

            return Ok(new { message = "¡Base de datos lista para PRODUCCIÓN! Datos antiguos borrados, habitaciones, menú y Admin creados." });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Error poblando base de datos", error = ex.Message, details = ex.InnerException?.Message });
        }
    }

    // --- MÉTODOS DE GENERACIÓN ---

    private async Task CreateProductionRooms()
    {
        var rooms = new List<Room>
        {
            // HABITACIÓN DOBLE ($130.000)
            CreateRoom("102", 1, "Doble", 130000m),
            CreateRoom("103", 1, "Doble", 130000m),
            CreateRoom("206", 2, "Doble", 130000m),
            CreateRoom("207", 2, "Doble", 130000m),

            // HABITACIÓN TRIPLE ($200.000)
            CreateRoom("204", 2, "Triple", 200000m),

            // HABITACIÓN FAMILIAR ($200.000)
            CreateRoom("101", 1, "Familiar", 200000m),
            CreateRoom("104", 1, "Familiar", 200000m),
            CreateRoom("201", 2, "Familiar", 200000m),
            CreateRoom("202", 2, "Familiar", 200000m),
            CreateRoom("203", 2, "Familiar", 200000m),
            CreateRoom("205", 2, "Familiar", 200000m),
            CreateRoom("208", 2, "Familiar", 200000m),
            CreateRoom("302", 3, "Familiar", 200000m),
            CreateRoom("303", 3, "Familiar", 200000m),
            CreateRoom("304", 3, "Familiar", 200000m),
            CreateRoom("305", 3, "Familiar", 200000m),
            CreateRoom("306", 3, "Familiar", 200000m),

            // HABITACIÓN SUITE FAMILIAR ($300.000)
            CreateRoom("301", 3, "Suite Familiar", 300000m),
            CreateRoom("307", 3, "Suite Familiar", 300000m)
        };

        await _context.Rooms.AddRangeAsync(rooms);
        await _context.SaveChangesAsync();
    }

    private Room CreateRoom(string number, int floor, string category, decimal price)
    {
        return new Room
        {
            Id = Guid.NewGuid(),
            Number = number,
            Floor = floor,
            Category = category,
            BasePrice = price,
            Status = RoomStatus.Available
        };
    }

    private async Task CreateProductionProducts()
    {
        // Imágenes genéricas para dar un buen aspecto inicial en el POS
        string imgSoda = "https://images.unsplash.com/photo-1622483767028-3f66f32aef97?q=80&w=600";
        string imgBeer = "https://images.unsplash.com/photo-1614316058562-b13c77d48d28?q=80&w=600";
        string imgWater = "https://images.unsplash.com/photo-1548839140-29a749e1bc4e?q=80&w=600";
        string imgLiquor = "https://images.unsplash.com/photo-1569529465841-dfecdab7503b?q=80&w=600";
        string imgWine = "https://images.unsplash.com/photo-1506377247377-2a5b3b417ebb?q=80&w=600";
        string imgSnack = "https://images.unsplash.com/photo-1621506289937-a8e4df240d0b?q=80&w=600";
        string imgHygiene = "https://images.unsplash.com/photo-1554372562-8d76e336b284?q=80&w=600";
        string imgCocktail = "https://images.unsplash.com/photo-1514362545857-3bc16c4c7d1b?q=80&w=600";
        string imgJuice = "https://images.unsplash.com/photo-1622597467836-f3285f2131b8?q=80&w=600";

        var products = new List<Product>
        {
            // ================== BEBIDAS NO ALCOHÓLICAS ==================
            CreateProduct("Soda Bretaña 300 ml", 5000, "Bebidas", imgSoda, true),
            CreateProduct("Agua Cristal 600 ml", 4000, "Bebidas", imgWater, true),
            CreateProduct("Coca Cola 500 ml", 5000, "Bebidas", imgSoda, true),
            CreateProduct("Electrolit 625 ml", 12000, "Bebidas", imgWater, true),
            CreateProduct("Postobón Manzana/Colombiana 400 ml", 5000, "Bebidas", imgSoda, true),
            CreateProduct("Gatorade 500 ml", 6000, "Bebidas", imgWater, true),
            CreateProduct("Jugo Hit 500 ml", 5000, "Bebidas", imgJuice, true),

            // ================== CERVEZAS ==================
            CreateProduct("Cerveza Coronita 210 ml", 7000, "Cervezas", imgBeer, true),
            CreateProduct("Cerveza Pilsen (Lata) 330 ml", 7000, "Cervezas", imgBeer, true),
            CreateProduct("Cerveza Aguila Light (Lata) 330 ml", 6000, "Cervezas", imgBeer, true),
            CreateProduct("Cerveza Club Colombia Dorada (Lata) 330 ml", 7000, "Cervezas", imgBeer, true),
            CreateProduct("Cerveza Poker (Lata) 330 ml", 6000, "Cervezas", imgBeer, true),
            CreateProduct("Cerveza Aguila Original (Lata) 330 ml", 6000, "Cervezas", imgBeer, true),
            CreateProduct("Refajo Cola y Pola 330 ml", 6000, "Cervezas", imgBeer, true),
            CreateProduct("Smirnoff Ice 250 ml", 12000, "Cervezas", imgBeer, true),

            // ================== LICORES (BOTELLAS Y MEDIAS) ==================
            CreateProduct("Buchanan's Deluxe", 220000, "Licores", imgLiquor, true),
            CreateProduct("Aguardiente Antioqueño Verde (Botella)", 100000, "Licores", imgLiquor, true),
            CreateProduct("Aguardiente Antioqueño Verde (Media)", 60000, "Licores", imgLiquor, true),
            CreateProduct("Aguardiente Amarillo (Botella)", 100000, "Licores", imgLiquor, true),
            CreateProduct("Aguardiente Amarillo (Media)", 60000, "Licores", imgLiquor, true),
            CreateProduct("Vino Gato Negro (Botella)", 80000, "Licores", imgWine, true),
            CreateProduct("Vino Casa (Botella)", 60000, "Licores", imgWine, true),
            CreateProduct("Whisky Deluxe (Botella)", 220000, "Licores", imgLiquor, true),
            CreateProduct("Ron (Botella)", 100000, "Licores", imgLiquor, true),
            CreateProduct("Ron (Media)", 60000, "Licores", imgLiquor, true),

            // ================== MECATO (SNACKS) ==================
            CreateProduct("Papas Margarita", 4000, "Mecato", imgSnack, true),
            CreateProduct("De Todito", 5000, "Mecato", imgSnack, true),
            CreateProduct("Galletas Festival", 3000, "Mecato", imgSnack, true),
            CreateProduct("Chicles Trident", 3000, "Mecato", imgSnack, true),

            // ================== ASEO PERSONAL ==================
            CreateProduct("Crema Dental Colgate 22 ml", 5000, "Aseo Personal", imgHygiene, true),
            CreateProduct("Cepillo de Dientes", 5000, "Aseo Personal", imgHygiene, true),
            CreateProduct("Shampoo Savital 25 ml (Sachet)", 3000, "Aseo Personal", imgHygiene, true),
            CreateProduct("Crema para peinar Savital 22 ml (Sachet)", 3000, "Aseo Personal", imgHygiene, true),

            // ================== CÓCTELES BAR ZAFIRO (NO TRACKEAN INVENTARIO) ==================
            CreateProduct("Piña Colada", 22000, "Cócteles", imgCocktail, false),
            CorrijaProduct("Mojito", 22000, "Cócteles", imgCocktail, false),
            CreateProduct("Caipiriña", 18000, "Cócteles", imgCocktail, false),
            CreateProduct("Tequila Sunrise", 22000, "Cócteles", imgCocktail, false),
            CreateProduct("Margarita", 22000, "Cócteles", imgCocktail, false),
            CreateProduct("Shot Aguardiente", 6000, "Cócteles", imgLiquor, false),
            CreateProduct("Shot Tequila", 6000, "Cócteles", imgLiquor, false),

            // ================== BEBIDAS REFRESCANTES (NO TRACKEAN INVENTARIO) ==================
            CreateProduct("Limonada Natural", 10000, "Refrescantes", imgCocktail, false),
            CreateProduct("Cerezada", 15000, "Refrescantes", imgCocktail, false),
            CreateProduct("Limonada de Coco", 15000, "Refrescantes", imgCocktail, false),
            CreateProduct("Limonada Coco-Hierbabuena", 16000, "Refrescantes", imgCocktail, false)
        };

        await _context.Products.AddRangeAsync(products);
        await _context.SaveChangesAsync();
    }

    private Product CreateProduct(string name, decimal price, string category, string imageUrl, bool isTracked)
    {
        return new Product
        {
            Id = Guid.NewGuid(),
            Name = name,
            Description = category,
            UnitPrice = price,
            Category = category,
            Stock = isTracked ? 50 : 0, // Inventario base de 50 si es producto físico
            IsActive = true,
            IsStockTracked = isTracked, 
            ImageUrl = imageUrl,
            CreatedAt = DateTime.UtcNow
        };
    }
    
    // Helper por error tipográfico en CreateProduct arriba
    private Product CorrijaProduct(string name, decimal price, string category, string imageUrl, bool isTracked)
    {
        return CreateProduct(name, price, category, imageUrl, isTracked);
    }
}