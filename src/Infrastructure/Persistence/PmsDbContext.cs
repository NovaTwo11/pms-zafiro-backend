using Microsoft.EntityFrameworkCore;
using PmsZafiro.Domain.Entities;

namespace PmsZafiro.Infrastructure.Persistence;

public class PmsDbContext : DbContext
{
    public PmsDbContext(DbContextOptions<PmsDbContext> options) : base(options) { }

    public DbSet<Room> Rooms { get; set; }
    public DbSet<Guest> Guests { get; set; }
    public DbSet<Reservation> Reservations { get; set; }
    public DbSet<Folio> Folios { get; set; }
    public DbSet<AppNotification> Notifications { get; set; }
    public DbSet<CashierShift> CashierShifts { get; set; }
    public DbSet<Product> Products { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        // Configuración de Herencia de Folios (Table-Per-Hierarchy)
        modelBuilder.Entity<Folio>()
            .HasDiscriminator<string>("FolioType")
            .HasValue<GuestFolio>("Guest")
            .HasValue<ExternalFolio>("External");
            
        // Relación Reserva -> Huésped (1 a N)
        modelBuilder.Entity<Reservation>()
            .HasOne(r => r.Guest)
            .WithMany(g => g.Reservations)
            .HasForeignKey(r => r.GuestId);

        // Relación Reserva -> Habitación
        modelBuilder.Entity<Reservation>()
            .HasOne(r => r.Room)
            .WithMany()
            .HasForeignKey(r => r.RoomId);
            
        // Relación Transacción -> Turno de Caja
        modelBuilder.Entity<FolioTransaction>()
            .HasOne(t => t.CashierShift)
            .WithMany(s => s.Transactions)
            .HasForeignKey(t => t.CashierShiftId)
            .OnDelete(DeleteBehavior.Restrict);
        
        // CONFIGURACIÓN DE DINERO (DECIMALES)
        var decimalProps = modelBuilder.Model
            .GetEntityTypes()
            .SelectMany(t => t.GetProperties())
            .Where(p => p.ClrType == typeof(decimal) || p.ClrType == typeof(decimal?));

        foreach (var property in decimalProps)
        {
            property.SetPrecision(18);
            property.SetScale(2);
        }
    }
}