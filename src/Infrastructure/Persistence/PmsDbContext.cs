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
    public DbSet<ReservationGuest> ReservationGuests { get; set; }
    
    // NUEVO: Agregamos el DbSet para que reconozca la tabla de variaciones de precios
    public DbSet<RoomPriceOverride> RoomPriceOverrides { get; set; }

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
        
        modelBuilder.Entity<ReservationGuest>()
            .HasKey(rg => new { rg.ReservationId, rg.GuestId }); // Clave compuesta

        modelBuilder.Entity<ReservationGuest>()
            .HasOne(rg => rg.Reservation)
            .WithMany(r => r.ReservationGuests)
            .HasForeignKey(rg => rg.ReservationId)
            .OnDelete(DeleteBehavior.Cascade); // Si borras la reserva, se borran los acompañantes de la lista

        modelBuilder.Entity<ReservationGuest>()
            .HasOne(rg => rg.Guest)
            .WithMany() // El Guest no necesita tener una lista explícita de reservas donde fue acompañante por ahora
            .HasForeignKey(rg => rg.GuestId)
            .OnDelete(DeleteBehavior.Restrict); // No borrar el perfil del huésped si se borra la reserva
        
        modelBuilder.Entity<GuestFolio>().ToTable("GuestFolios");
        modelBuilder.Entity<ExternalFolio>().ToTable("ExternalFolios");
    }
}