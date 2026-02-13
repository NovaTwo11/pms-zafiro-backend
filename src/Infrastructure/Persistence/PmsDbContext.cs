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
    public DbSet<FolioTransaction> FolioTransactions { get; set; }
    public DbSet<AppNotification> Notifications { get; set; }
    public DbSet<CashierShift> CashierShifts { get; set; }
    public DbSet<Product> Products { get; set; }
    public DbSet<ReservationSegment> ReservationSegments { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    base.OnModelCreating(modelBuilder);

    // --- CONFIGURACIÓN DE RESERVAS (NUEVA ARQUITECTURA) ---
    
    // 1. La Reserva ya no tiene RoomId directo. Tiene una colección de segmentos.
    modelBuilder.Entity<Reservation>()
        .HasMany(r => r.Segments)
        .WithOne(s => s.Reservation)
        .HasForeignKey(s => s.ReservationId)
        .OnDelete(DeleteBehavior.Cascade); // Si borras la reserva, se borran los segmentos

    // 2. Configuración del Segmento (La tabla intermedia que tiene el RoomId)
    modelBuilder.Entity<ReservationSegment>(entity =>
    {
        entity.ToTable("ReservationSegments");
        entity.HasKey(s => s.Id);

        // Relación Segmento -> Habitación
        entity.HasOne(s => s.Room)
            .WithMany() // Una habitación puede tener muchos segmentos (historial)
            .HasForeignKey(s => s.RoomId)
            .OnDelete(DeleteBehavior.Restrict); // No borrar habitación si tiene historial
    });

    // --- OTRAS CONFIGURACIONES EXISTENTES (MANTENER) ---
    
    modelBuilder.Entity<GuestFolio>().ToTable("GuestFolios");
    modelBuilder.Entity<ExternalFolio>().ToTable("ExternalFolios");
    
    // (Asegúrate de borrar el bloque antiguo modelBuilder.Entity<Reservation>().HasOne(r => r.Room)...)
}
}