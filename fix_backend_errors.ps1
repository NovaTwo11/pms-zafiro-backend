# fix_backend_errors.ps1
$ErrorActionPreference = "Stop"

function Write-File {
    param([string]$Path, [string]$Content)
    $Dir = [System.IO.Path]::GetDirectoryName($Path)
    if (!(Test-Path $Dir)) { New-Item -ItemType Directory -Force -Path $Dir | Out-Null }
    [System.IO.File]::WriteAllText($Path, $Content)
    Write-Host "OK: $Path fixed." -ForegroundColor Green
}

Write-Host "Fixing Backend Compilation Errors..." -ForegroundColor Cyan

# 1. Corregir Entidad Reservation (Asegurar propiedad Guest)
$reservation = @"
using PmsZafiro.Domain.Enums;
namespace PmsZafiro.Domain.Entities;

public class Reservation
{
    public Guid Id { get; set; }
    public string ConfirmationCode { get; set; } = string.Empty;
    
    public Guid GuestId { get; set; }
    public Guest Guest { get; set; } = null!; // Propiedad de navegacion requerida
    
    public Guid RoomId { get; set; }
    public Room Room { get; set; } = null!;

    public DateTime CheckIn { get; set; }
    public DateTime CheckOut { get; set; }
    
    public ReservationStatus Status { get; set; } = ReservationStatus.Pending;
    public int Adults { get; set; }
    public int Children { get; set; }
    public decimal TotalAmount { get; set; }
    public string? Notes { get; set; }
    
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
"@
Write-File "src/Domain/Entities/Reservation.cs" $reservation

# 2. Corregir PmsDbContext (Eliminar referencias rotas)
$context = @"
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
"@
Write-File "src/Infrastructure/Persistence/PmsDbContext.cs" $context

# 3. Corregir ReservationRepository (Eliminar uso de ReservationGuests si no existe)
# Asumiremos que solo necesitamos consultar Reservas con sus Huespedes principales
$repo = @"
using Microsoft.EntityFrameworkCore;
using PmsZafiro.Application.Interfaces;
using PmsZafiro.Domain.Entities;
using PmsZafiro.Domain.Enums;
using PmsZafiro.Infrastructure.Persistence;

namespace PmsZafiro.Infrastructure.Repositories;

public class ReservationRepository : IReservationRepository
{
    private readonly PmsDbContext _context;

    public ReservationRepository(PmsDbContext context)
    {
        _context = context;
    }

    public async Task<Reservation?> GetByIdAsync(Guid id)
    {
        return await _context.Reservations
            .Include(r => r.Guest)
            .Include(r => r.Room)
            .FirstOrDefaultAsync(r => r.Id == id);
    }

    public async Task<IEnumerable<Reservation>> GetAllAsync()
    {
        return await _context.Reservations
            .Include(r => r.Guest)
            .Include(r => r.Room)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();
    }

    public async Task AddAsync(Reservation reservation)
    {
        await _context.Reservations.AddAsync(reservation);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(Reservation reservation)
    {
        _context.Reservations.Update(reservation);
        await _context.SaveChangesAsync();
    }
    
    public async Task<IEnumerable<Reservation>> GetActiveReservationsByRoomAsync(Guid roomId)
    {
         return await _context.Reservations
            .Where(r => r.RoomId == roomId && 
                       (r.Status == ReservationStatus.Confirmed || r.Status == ReservationStatus.CheckedIn))
            .ToListAsync();
    }
}
"@
Write-File "src/Infrastructure/Repositories/ReservationRepository.cs" $repo

Write-Host "Done. Try 'dotnet build' again." -ForegroundColor Yellow