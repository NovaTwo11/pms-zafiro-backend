# setup_cashiering_ascii.ps1
# Version ASCII Safe - Sin acentos para evitar errores de codificacion
$ErrorActionPreference = "Stop"

function Write-File {
    param([string]$Path, [string]$Content)
    $Dir = [System.IO.Path]::GetDirectoryName($Path)
    if (!(Test-Path $Dir)) { New-Item -ItemType Directory -Force -Path $Dir | Out-Null }
    [System.IO.File]::WriteAllText($Path, $Content)
    Write-Host "OK: $Path created/updated." -ForegroundColor Green
}

Write-Host "Starting Cashiering Setup..." -ForegroundColor Cyan

# 1. ENUMS
$enums = @"
namespace PmsZafiro.Domain.Enums;

public enum RoomStatus { Available = 0, Occupied = 1, Dirty = 2, Maintenance = 3, TouchUp = 4, Blocked = 5 }
public enum ReservationStatus { Pending, Confirmed, CheckedIn, CheckedOut, Cancelled, NoShow }
public enum FolioStatus { Open, Closed }
public enum IdType { CC, CE, PA, TI, RC }
public enum TransactionType { Charge, Payment, Adjustment }
public enum NotificationType { Info, Success, Warning, Error }
public enum PaymentMethod { None = 0, Cash = 1, CreditCard = 2, DebitCard = 3, Transfer = 4 }
public enum CashierShiftStatus { Open, Closed }
"@
Write-File "src/Domain/Enums/Enums.cs" $enums

# 2. ENTITIES
$shift = @"
using PmsZafiro.Domain.Enums;
namespace PmsZafiro.Domain.Entities;

public class CashierShift
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public DateTimeOffset OpenedAt { get; set; }
    public DateTimeOffset? ClosedAt { get; set; }
    public decimal StartingAmount { get; set; }
    public decimal SystemCalculatedAmount { get; set; }
    public decimal ActualAmount { get; set; }
    public CashierShiftStatus Status { get; set; } = CashierShiftStatus.Open;
    public ICollection<FolioTransaction> Transactions { get; set; } = new List<FolioTransaction>();
}
"@
Write-File "src/Domain/Entities/CashierShift.cs" $shift

$folio = @"
using PmsZafiro.Domain.Enums;
namespace PmsZafiro.Domain.Entities;

public abstract class Folio
{
    public Guid Id { get; set; }
    public FolioStatus Status { get; set; } = FolioStatus.Open;    
    public ICollection<FolioTransaction> Transactions { get; set; } = new List<FolioTransaction>();
    public decimal Balance => Transactions.Where(t => t.Type == TransactionType.Charge).Sum(t => t.Amount) - 
                              Transactions.Where(t => t.Type == TransactionType.Payment).Sum(t => t.Amount);
}

public class GuestFolio : Folio
{
    public Guid ReservationId { get; set; }
    public Reservation Reservation { get; set; } = null!;
}

public class ExternalFolio : Folio
{
    public string Alias { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

public class FolioTransaction
{
    public Guid Id { get; set; }
    public Guid FolioId { get; set; }
    public TransactionType Type { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public int Quantity { get; set; } = 1;
    public decimal UnitPrice { get; set; }
    
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.None;
    public Guid? CashierShiftId { get; set; }
    public CashierShift? CashierShift { get; set; }
    
    public DateTimeOffset CreatedAt { get; set; }
    public string CreatedByUserId { get; set; } = string.Empty;
}
"@
Write-File "src/Domain/Entities/Folio.cs" $folio

# 3. DB CONTEXT
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
        
        modelBuilder.Entity<Folio>().HasDiscriminator<string>("FolioType").HasValue<GuestFolio>("Guest").HasValue<ExternalFolio>("External");
        modelBuilder.Entity<Reservation>().HasOne(r => r.Guest).WithMany(g => g.Reservations).HasForeignKey(r => r.GuestId);
        modelBuilder.Entity<Reservation>().HasOne(r => r.Room).WithMany().HasForeignKey(r => r.RoomId);
        modelBuilder.Entity<FolioTransaction>().HasOne<Folio>().WithMany(f => f.Transactions).HasForeignKey(t => t.FolioId);
        modelBuilder.Entity<FolioTransaction>().HasOne(t => t.CashierShift).WithMany(s => s.Transactions).HasForeignKey(t => t.CashierShiftId).OnDelete(DeleteBehavior.Restrict);
    }
}
"@
Write-File "src/Infrastructure/Persistence/PmsDbContext.cs" $context

# 4. APPLICATION LAYER
Write-File "src/Application/DTOs/Cashier/OpenShiftDto.cs" "namespace PmsZafiro.Application.DTOs.Cashier; public record OpenShiftDto(decimal StartingAmount);"
Write-File "src/Application/DTOs/Cashier/CloseShiftDto.cs" "namespace PmsZafiro.Application.DTOs.Cashier; public record CloseShiftDto(decimal ActualAmount);"
Write-File "src/Application/DTOs/Cashier/CashierShiftDto.cs" "using PmsZafiro.Domain.Enums; namespace PmsZafiro.Application.DTOs.Cashier; public record CashierShiftDto(Guid Id, string UserId, DateTimeOffset OpenedAt, DateTimeOffset? ClosedAt, decimal StartingAmount, decimal SystemCalculatedAmount, decimal ActualAmount, CashierShiftStatus Status);"

$repoInterface = @"
using PmsZafiro.Domain.Entities;
namespace PmsZafiro.Application.Interfaces;
public interface ICashierRepository
{
    Task<CashierShift?> GetOpenShiftByUserIdAsync(string userId);
    Task AddShiftAsync(CashierShift shift);
    Task UpdateShiftAsync(CashierShift shift);
    Task<CashierShift?> GetShiftByIdAsync(Guid id);
}
"@
Write-File "src/Application/Interfaces/ICashierRepository.cs" $repoInterface

$repoImpl = @"
using Microsoft.EntityFrameworkCore;
using PmsZafiro.Application.Interfaces;
using PmsZafiro.Domain.Entities;
using PmsZafiro.Domain.Enums;
using PmsZafiro.Infrastructure.Persistence;

namespace PmsZafiro.Infrastructure.Repositories;

public class CashierRepository : ICashierRepository
{
    private readonly PmsDbContext _context;
    public CashierRepository(PmsDbContext context) { _context = context; }

    public async Task<CashierShift?> GetOpenShiftByUserIdAsync(string userId)
    {
        return await _context.CashierShifts.Include(s => s.Transactions).FirstOrDefaultAsync(s => s.UserId == userId && s.Status == CashierShiftStatus.Open);
    }
    public async Task AddShiftAsync(CashierShift shift) { _context.CashierShifts.Add(shift); await _context.SaveChangesAsync(); }
    public async Task UpdateShiftAsync(CashierShift shift) { _context.CashierShifts.Update(shift); await _context.SaveChangesAsync(); }
    public async Task<CashierShift?> GetShiftByIdAsync(Guid id) { return await _context.CashierShifts.FindAsync(id); }
}
"@
Write-File "src/Infrastructure/Repositories/CashierRepository.cs" $repoImpl

$service = @"
using PmsZafiro.Application.DTOs.Cashier;
using PmsZafiro.Application.Interfaces;
using PmsZafiro.Domain.Entities;
using PmsZafiro.Domain.Enums;

namespace PmsZafiro.Application.Services;

public class CashierService
{
    private readonly ICashierRepository _repository;
    public CashierService(ICashierRepository repository) { _repository = repository; }

    public async Task<CashierShiftDto?> GetStatusAsync(string userId)
    {
        var shift = await _repository.GetOpenShiftByUserIdAsync(userId);
        return shift == null ? null : MapToDto(shift);
    }

    public async Task<CashierShiftDto> OpenShiftAsync(string userId, decimal startingAmount)
    {
        var existing = await _repository.GetOpenShiftByUserIdAsync(userId);
        if (existing != null) throw new InvalidOperationException(""Shift already open."");
        var shift = new CashierShift { Id = Guid.NewGuid(), UserId = userId, OpenedAt = DateTimeOffset.UtcNow, StartingAmount = startingAmount, Status = CashierShiftStatus.Open };
        await _repository.AddShiftAsync(shift);
        return MapToDto(shift);
    }

    public async Task<CashierShiftDto> CloseShiftAsync(string userId, decimal actualAmount)
    {
        var shift = await _repository.GetOpenShiftByUserIdAsync(userId);
        if (shift == null) throw new InvalidOperationException(""No open shift found."");
        
        var totalPayments = shift.Transactions.Where(t => t.Type == TransactionType.Payment).Sum(t => t.Amount);
        shift.SystemCalculatedAmount = shift.StartingAmount + totalPayments;
        shift.ActualAmount = actualAmount;
        shift.ClosedAt = DateTimeOffset.UtcNow;
        shift.Status = CashierShiftStatus.Closed;
        
        await _repository.UpdateShiftAsync(shift);
        return MapToDto(shift);
    }

    private static CashierShiftDto MapToDto(CashierShift s) => new(s.Id, s.UserId, s.OpenedAt, s.ClosedAt, s.StartingAmount, s.SystemCalculatedAmount, s.ActualAmount, s.Status);
}
"@
Write-File "src/Application/Services/CashierService.cs" $service

# 5. API CONTROLLER
$controller = @"
using Microsoft.AspNetCore.Mvc;
using PmsZafiro.Application.DTOs.Cashier;
using PmsZafiro.Application.Services;

namespace PmsZafiro.API.Controllers;

[ApiController]
[Route(""api/[controller]"")]
public class CashierController : ControllerBase
{
    private readonly CashierService _service;
    public CashierController(CashierService service) { _service = service; }

    [HttpGet(""status"")]
    public async Task<ActionResult<CashierShiftDto>> GetStatus()
    {
        var status = await _service.GetStatusAsync(""user1"");
        if (status == null) return NoContent();
        return Ok(status);
    }

    [HttpPost(""open"")]
    public async Task<ActionResult<CashierShiftDto>> OpenShift([FromBody] OpenShiftDto dto)
    {
        try { return Ok(await _service.OpenShiftAsync(""user1"", dto.StartingAmount)); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
    }

    [HttpPost(""close"")]
    public async Task<ActionResult<CashierShiftDto>> CloseShift([FromBody] CloseShiftDto dto)
    {
        try { return Ok(await _service.CloseShiftAsync(""user1"", dto.ActualAmount)); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
    }
}
"@
Write-File "src/API/Controllers/CashierController.cs" $controller

# 6. PROGRAM.CS DI
$programPath = "src/API/Program.cs"
$programContent = Get-Content $programPath -Raw
if (-not ($programContent -match "CashierService")) {
    $programContent = $programContent.Replace("builder.Services.AddScoped<IFolioRepository, FolioRepository>();", "builder.Services.AddScoped<IFolioRepository, FolioRepository>();`nbuilder.Services.AddScoped<ICashierRepository, CashierRepository>();`nbuilder.Services.AddScoped<CashierService>();")
    [System.IO.File]::WriteAllText($programPath, $programContent)
    Write-Host "Program.cs updated with DI." -ForegroundColor Green
}

# 7. MIGRATIONS
Write-Host "Running Migrations..." -ForegroundColor Yellow
dotnet ef migrations add AddCashieringV4 -p src/Infrastructure -s src/API
if ($LASTEXITCODE -eq 0) {
    dotnet ef database update -p src/Infrastructure -s src/API
    Write-Host "Database updated successfully!" -ForegroundColor Green
} else {
    Write-Host "Error creating migration. Check if it already exists." -ForegroundColor Red
}