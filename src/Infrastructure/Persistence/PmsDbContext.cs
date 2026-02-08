using Microsoft.EntityFrameworkCore;
using PmsZafiro.Domain.Entities;
using PmsZafiro.Domain.Enums;

namespace PmsZafiro.Infrastructure.Persistence;

public class PmsDbContext : DbContext
{
    public PmsDbContext(DbContextOptions<PmsDbContext> options) : base(options)
    {
    }

    // --- TABLAS DE LA BASE DE DATOS ---
    public DbSet<Guest> Guests => Set<Guest>();
    public DbSet<Room> Rooms => Set<Room>();
    public DbSet<Reservation> Reservations => Set<Reservation>();
    public DbSet<ReservationGuestDetail> ReservationGuests => Set<ReservationGuestDetail>();
    public DbSet<Folio> Folios => Set<Folio>();
    public DbSet<FolioTransaction> Transactions => Set<FolioTransaction>();
    public DbSet<AppNotification> Notifications => Set<AppNotification>();
    public DbSet<RoomPriceOverride> RoomPrices => Set<RoomPriceOverride>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    base.OnModelCreating(modelBuilder);

    // --- CONFIGURACIÓN DE DINERO (CRÍTICO) ---
    // Definimos precisión de 18 dígitos con 2 decimales para evitar errores de redondeo
    modelBuilder.Entity<Room>().Property(r => r.BasePrice).HasPrecision(18, 2);
    modelBuilder.Entity<RoomPriceOverride>().Property(r => r.Price).HasPrecision(18, 2);
    // El balance es calculado, no se guarda en BD
    modelBuilder.Entity<Folio>().Ignore(f => f.Balance); 
    modelBuilder.Entity<FolioTransaction>().Property(t => t.Amount).HasPrecision(18, 2);
    modelBuilder.Entity<FolioTransaction>().Property(t => t.UnitPrice).HasPrecision(18, 2);

    // --- CONFIGURACIÓN DE ENUMS (AUDITORÍA) ---
    // Guardamos los Enums como Strings para que sean legibles en SQL
    modelBuilder.Entity<Guest>().Property(g => g.DocumentType).HasConversion<string>();
    modelBuilder.Entity<Room>().Property(r => r.Status).HasConversion<string>();
    modelBuilder.Entity<Reservation>().Property(r => r.Status).HasConversion<string>();
    modelBuilder.Entity<FolioTransaction>().Property(t => t.Type).HasConversion<string>();
    modelBuilder.Entity<AppNotification>().Property(n => n.Type).HasConversion<string>();
    // IMPORTANTE: Agregamos FolioStatus que faltaba en tu snippet original
    modelBuilder.Entity<Folio>().Property(f => f.Status).HasConversion<string>(); 

    // --- RELACIONES COMPLEJAS ---
    
    // 1. Configurar la clave compuesta de la tabla intermedia (Muchos a Muchos)
    modelBuilder.Entity<ReservationGuestDetail>()
        .HasKey(rg => new { rg.ReservationId, rg.GuestId });

    // 2. Relación Reserva -> Detalles (Si borro Reserva, SE BORRAN los detalles)
    modelBuilder.Entity<ReservationGuestDetail>()
        .HasOne(rg => rg.Reservation)
        .WithMany(r => r.Guests)
        .HasForeignKey(rg => rg.ReservationId)
        .OnDelete(DeleteBehavior.Cascade); 

    // 3. Relación Huésped -> Detalles (Si borro Huésped, NO PERMITIR si tiene historial)
    // *** AQUÍ ESTÁ EL FIX DEL ERROR DE CICLOS ***
    modelBuilder.Entity<ReservationGuestDetail>()
        .HasOne(rg => rg.Guest)
        .WithMany()
        .HasForeignKey(rg => rg.GuestId)
        .OnDelete(DeleteBehavior.Restrict); 
        
    // Herencia de Folios (Discriminator)
    // Esto permite que GuestFolio y ExternalFolio vivan en la misma tabla "Folios"
    modelBuilder.Entity<Folio>()
        .HasDiscriminator<string>("FolioType")
        .HasValue<GuestFolio>("Guest")
        .HasValue<ExternalFolio>("External");
}

    // --- AUTOMATIZACIÓN DE FECHAS ---
    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var entry in ChangeTracker.Entries())
        {
            // Si es una entidad nueva y tiene propiedad CreatedAt, poner la fecha actual
            if (entry.State == EntityState.Added)
            {
                var createdAtProp = entry.Entity.GetType().GetProperty("CreatedAt");
                if (createdAtProp != null && createdAtProp.PropertyType == typeof(DateTimeOffset))
                {
                    createdAtProp.SetValue(entry.Entity, DateTimeOffset.UtcNow);
                }
            }
            
            // Si se edita, actualizar UpdatedAt (si existe)
            if (entry.State == EntityState.Modified)
            {
                var updatedAtProp = entry.Entity.GetType().GetProperty("UpdatedAt");
                if (updatedAtProp != null && updatedAtProp.PropertyType == typeof(DateTimeOffset?))
                {
                    updatedAtProp.SetValue(entry.Entity, DateTimeOffset.UtcNow);
                }
            }
        }

        return base.SaveChangesAsync(cancellationToken);
    }
}
