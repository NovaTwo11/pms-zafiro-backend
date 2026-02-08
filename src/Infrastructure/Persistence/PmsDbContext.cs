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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        // Configuracion de Herencia de Folios
        modelBuilder.Entity<Folio>()
            .HasDiscriminator<string>("FolioType")
            .HasValue<GuestFolio>("Guest")
            .HasValue<ExternalFolio>("External");
            
        // Relacion Reserva -> Huesped
        modelBuilder.Entity<Reservation>()
            .HasOne(r => r.Guest)
            .WithMany(g => g.Reservations)
            .HasForeignKey(r => r.GuestId);

        // Relacion Reserva -> Habitacion
        modelBuilder.Entity<Reservation>()
            .HasOne(r => r.Room)
            .WithMany()
            .HasForeignKey(r => r.RoomId);
            
        // Relacion Transaccion -> Caja
        modelBuilder.Entity<FolioTransaction>()
            .HasOne(t => t.CashierShift)
            .WithMany(s => s.Transactions)
            .HasForeignKey(t => t.CashierShiftId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}