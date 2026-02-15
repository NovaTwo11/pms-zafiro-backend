// PmsZafiro.Infrastructure/Persistence/PmsDbContext.cs
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
    public DbSet<User> Users { get; set; }
    public DbSet<RoomPriceOverride> RoomPriceOverrides { get; set; }
    public DbSet<ChannelRoomMapping> ChannelRoomMappings { get; set; }
    public DbSet<IntegrationInboundEvent> IntegrationInboundEvents { get; set; }
    public DbSet<IntegrationOutboundEvent> IntegrationOutboundEvents { get; set; }
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // --- CONFIGURACIÓN DE RESERVAS (NUEVA ARQUITECTURA) ---
        
        modelBuilder.Entity<Reservation>()
            .HasMany(r => r.Segments)
            .WithOne(s => s.Reservation)
            .HasForeignKey(s => s.ReservationId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ReservationSegment>(entity =>
        {
            entity.ToTable("ReservationSegments");
            entity.HasKey(s => s.Id);
            entity.HasOne(s => s.Room)
                .WithMany() 
                .HasForeignKey(s => s.RoomId)
                .OnDelete(DeleteBehavior.Restrict); 
        });
        
        modelBuilder.Entity<ReservationGuest>()
            .HasKey(rg => new { rg.ReservationId, rg.GuestId });

        modelBuilder.Entity<ReservationGuest>()
            .HasOne(rg => rg.Reservation)
            .WithMany(r => r.ReservationGuests)
            .HasForeignKey(rg => rg.ReservationId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ReservationGuest>()
            .HasOne(rg => rg.Guest)
            .WithMany() 
            .HasForeignKey(rg => rg.GuestId)
            .OnDelete(DeleteBehavior.Restrict); 
        
        modelBuilder.Entity<GuestFolio>().ToTable("GuestFolios");
        modelBuilder.Entity<ExternalFolio>().ToTable("ExternalFolios");

        // --- CONFIGURACIÓN DE MAPEOS DE CANALES (OTA) ---
        modelBuilder.Entity<ChannelRoomMapping>(entity =>
        {
            entity.ToTable("ChannelRoomMappings");
            entity.HasKey(e => e.Id);
            
            // Índice único para evitar mapeos duplicados y buscar súper rápido cuando entra un Webhook
            entity.HasIndex(e => new { e.Channel, e.ExternalRoomId, e.ExternalRatePlanId }).IsUnique();
        });
        
        modelBuilder.Entity<User>().HasData(
            new User 
            { 
                Id = 1, 
                Username = "admin", 
                PasswordHash = "admin123", 
                Role = "Admin" ,
                CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            }
        );
    }
}