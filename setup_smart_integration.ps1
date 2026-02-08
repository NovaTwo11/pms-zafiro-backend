# fix_integration.ps1
# Este script utiliza un formato seguro para evitar errores de espacios en blanco

Write-Host "🚀 Iniciando reparación de integración (Versión Robusta)..." -ForegroundColor Cyan

$baseDir = "src"

function Escribir-Archivo {
    param (
        [string]$Path,
        [string]$Content
    )
    $FullDir = Split-Path $Path
    if (-not (Test-Path $FullDir)) {
        New-Item -ItemType Directory -Path $FullDir | Out-Null
    }
    Set-Content -Path $Path -Value $Content -Encoding UTF8
    Write-Host "✅ Archivo actualizado: $Path" -ForegroundColor Green
}

# ---------------------------------------------------------
# 1. ACTUALIZAR ENTIDAD GUEST
# ---------------------------------------------------------
$guestContent = @"
using PmsZafiro.Domain.Enums;

namespace PmsZafiro.Domain.Entities;

public class Guest
{
    public Guid Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string FullName => $"{FirstName} {LastName}";
    
    public IdType DocumentType { get; set; }
    public string DocumentNumber { get; set; } = string.Empty;
    
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Nationality { get; set; } = string.Empty;
    public DateOnly? BirthDate { get; set; }
    
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<Reservation> Reservations { get; set; } = new List<Reservation>();
}
"@
Escribir-Archivo -Path (Join-Path $baseDir "Domain/Entities/Guest.cs") -Content $guestContent

# ---------------------------------------------------------
# 2. ACTUALIZAR INTERFAZ IGuestRepository
# ---------------------------------------------------------
$iGuestContent = @"
using PmsZafiro.Domain.Entities;

namespace PmsZafiro.Application.Interfaces;

public interface IGuestRepository
{
    Task<IEnumerable<Guest>> GetAllAsync();
    Task<IEnumerable<Guest>> GetAllWithHistoryAsync(); 
    Task<Guest?> GetByIdAsync(Guid id);
    Task<Guest?> GetByDocumentAsync(string documentNumber);
    Task AddAsync(Guest guest);
    Task UpdateAsync(Guest guest);
    Task DeleteAsync(Guid id);
}
"@
Escribir-Archivo -Path (Join-Path $baseDir "Application/Interfaces/IGuestRepository.cs") -Content $iGuestContent

# ---------------------------------------------------------
# 3. ACTUALIZAR GuestRepository
# ---------------------------------------------------------
$guestRepoContent = @"
using Microsoft.EntityFrameworkCore;
using PmsZafiro.Application.Interfaces;
using PmsZafiro.Domain.Entities;
using PmsZafiro.Infrastructure.Persistence;

namespace PmsZafiro.Infrastructure.Repositories;

public class GuestRepository : IGuestRepository
{
    private readonly PmsDbContext _context;

    public GuestRepository(PmsDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<Guest>> GetAllAsync()
    {
        return await _context.Guests.ToListAsync();
    }

    public async Task<IEnumerable<Guest>> GetAllWithHistoryAsync()
    {
        return await _context.Guests
            .Include(g => g.Reservations)
            .OrderByDescending(g => g.CreatedAt)
            .ToListAsync();
    }

    public async Task<Guest?> GetByIdAsync(Guid id)
    {
        return await _context.Guests.FindAsync(id);
    }

    public async Task<Guest?> GetByDocumentAsync(string documentNumber)
    {
        return await _context.Guests
            .FirstOrDefaultAsync(g => g.DocumentNumber == documentNumber);
    }

    public async Task AddAsync(Guest guest)
    {
        await _context.Guests.AddAsync(guest);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(Guest guest)
    {
        _context.Guests.Update(guest);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(Guid id)
    {
        var guest = await _context.Guests.FindAsync(id);
        if (guest != null)
        {
            _context.Guests.Remove(guest);
            await _context.SaveChangesAsync();
        }
    }
}
"@
Escribir-Archivo -Path (Join-Path $baseDir "Infrastructure/Repositories/GuestRepository.cs") -Content $guestRepoContent

# ---------------------------------------------------------
# 4. ACTUALIZAR DTOs
# ---------------------------------------------------------
$guestDtoContent = @"
namespace PmsZafiro.Application.DTOs.Guests;

public class GuestDto
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string DocumentType { get; set; } = string.Empty;
    public string DocumentNumber { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Nationality { get; set; } = string.Empty;
    
    public int TotalStays { get; set; }
    public DateTime? LastStayDate { get; set; }
    public string CurrentStatus { get; set; } = ""previous"";
}
"@
Escribir-Archivo -Path (Join-Path $baseDir "Application/DTOs/Guests/GuestDto.cs") -Content $guestDtoContent

$bookingDtoContent = @"
namespace PmsZafiro.Application.DTOs.Reservations;

public class CreateBookingRequestDto
{
    public string? GuestName { get; set; }
    public string? GuestEmail { get; set; }
    public string? GuestPhone { get; set; }
    public string? DocType { get; set; }
    public string? DocNumber { get; set; }

    public Guid RoomId { get; set; }
    public DateOnly CheckIn { get; set; }
    public DateOnly CheckOut { get; set; }
    public int Adults { get; set; }
    public int Children { get; set; }
    public string? Notes { get; set; }
}
"@
Escribir-Archivo -Path (Join-Path $baseDir "Application/DTOs/Reservations/CreateBookingRequestDto.cs") -Content $bookingDtoContent

# ---------------------------------------------------------
# 5. ACTUALIZAR CONTROLADORES
# ---------------------------------------------------------
$guestsCtrlContent = @"
using Microsoft.AspNetCore.Mvc;
using PmsZafiro.Application.DTOs.Guests;
using PmsZafiro.Application.Interfaces;
using PmsZafiro.Domain.Entities;
using PmsZafiro.Domain.Enums;

namespace PmsZafiro.API.Controllers;

[ApiController]
[Route(""api/[controller]"")] 
public class GuestsController : ControllerBase
{
    private readonly IGuestRepository _repository;

    public GuestsController(IGuestRepository repository)
    {
        _repository = repository;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<GuestDto>>> GetAll()
    {
        var guests = await _repository.GetAllWithHistoryAsync();
        
        var dtos = guests.Select(g => {
            var lastRes = g.Reservations.OrderByDescending(r => r.EndDate).FirstOrDefault();
            var isActive = g.Reservations.Any(r => r.Status == ReservationStatus.CheckedIn);

            return new GuestDto
            {
                Id = g.Id,
                FullName = g.FullName,
                DocumentType = g.DocumentType.ToString(),
                DocumentNumber = g.DocumentNumber,
                Email = g.Email,
                Phone = g.Phone,
                Nationality = g.Nationality,
                TotalStays = g.Reservations.Count(r => r.Status != ReservationStatus.Cancelled),
                LastStayDate = lastRes?.EndDate.ToDateTime(TimeOnly.MinValue),
                CurrentStatus = isActive ? ""in-house"" : ""previous""
            };
        });

        return Ok(dtos);
    }

    [HttpGet(""{id}"")]
    public async Task<ActionResult<GuestDto>> GetById(Guid id)
    {
        var guest = await _repository.GetByIdAsync(id);
        if (guest == null) return NotFound();

        return Ok(new GuestDto
        {
            Id = guest.Id,
            FullName = guest.FullName,
            DocumentType = guest.DocumentType.ToString(),
            DocumentNumber = guest.DocumentNumber,
            Email = guest.Email,
            Phone = guest.Phone,
            Nationality = guest.Nationality
        });
    }

    [HttpPost]
    public async Task<ActionResult<GuestDto>> Create(CreateGuestDto dto)
    {
        var guest = new Guest
        {
            FirstName = dto.FirstName,
            LastName = dto.LastName,
            DocumentType = dto.DocumentType,
            DocumentNumber = dto.DocumentNumber,
            Email = dto.Email,
            Phone = dto.Phone,
            Nationality = dto.Nationality,
            BirthDate = dto.BirthDate
        };

        await _repository.AddAsync(guest);
        return CreatedAtAction(nameof(GetById), new { id = guest.Id }, guest);
    }
}
"@
Escribir-Archivo -Path (Join-Path $baseDir "API/Controllers/GuestsController.cs") -Content $guestsCtrlContent

$resCtrlContent = @"
using Microsoft.AspNetCore.Mvc;
using PmsZafiro.Application.DTOs.Reservations;
using PmsZafiro.Application.Interfaces;
using PmsZafiro.Domain.Entities;
using PmsZafiro.Domain.Enums;

namespace PmsZafiro.API.Controllers;

[ApiController]
[Route(""api/[controller]"")]
public class ReservationsController : ControllerBase
{
    private readonly IReservationRepository _repository;
    private readonly IFolioRepository _folioRepository;
    private readonly IRoomRepository _roomRepository;
    private readonly INotificationRepository _notificationRepository;
    private readonly IGuestRepository _guestRepository;

    public ReservationsController(
        IReservationRepository repository,
        IFolioRepository folioRepository,
        IRoomRepository roomRepository,
        INotificationRepository notificationRepository,
        IGuestRepository guestRepository)
    {
        _repository = repository;
        _folioRepository = folioRepository;
        _roomRepository = roomRepository;
        _notificationRepository = notificationRepository;
        _guestRepository = guestRepository;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<ReservationDto>>> GetAll()
    {
        var reservations = await _repository.GetAllAsync();
        var dtos = reservations.Select(r => new ReservationDto
        {
            Id = r.Id,
            Code = r.Code,
            Status = r.Status.ToString(),
            MainGuestId = r.MainGuestId,
            MainGuestName = r.MainGuest != null ? r.MainGuest.FullName : ""Sin Nombre"",
            RoomId = r.RoomId,
            RoomNumber = r.Room != null ? r.Room.Number : ""?"",
            StartDate = r.StartDate,
            EndDate = r.EndDate,
            Nights = r.Nights,
            HasFolio = true
        });
        return Ok(dtos);
    }

    [HttpGet(""{id}"")]
    public async Task<ActionResult<ReservationDto>> GetById(Guid id)
    {
        var r = await _repository.GetByIdAsync(id);
        if (r == null) return NotFound();

        var dto = new ReservationDto
        {
            Id = r.Id,
            Code = r.Code,
            Status = r.Status.ToString(),
            MainGuestId = r.MainGuestId,
            MainGuestName = r.MainGuest?.FullName ?? ""Desconocido"",
            RoomId = r.RoomId,
            RoomNumber = r.Room?.Number ?? ""?"",
            StartDate = r.StartDate,
            EndDate = r.EndDate,
            Nights = r.Nights,
            HasFolio = true
        };
        return Ok(dto);
    }

    [HttpPost]
    public async Task<ActionResult<ReservationDto>> Create(CreateReservationDto dto)
    {
        var reservation = new Reservation
        {
            MainGuestId = dto.MainGuestId,
            RoomId = dto.RoomId,
            StartDate = dto.StartDate,
            EndDate = dto.EndDate,
            Status = ReservationStatus.Pending,
            CreatedAt = DateTimeOffset.UtcNow
        };

        await _repository.CreateAsync(reservation);
        return CreatedAtAction(nameof(GetById), new { id = reservation.Id }, new { id = reservation.Id, code = reservation.Code });
    }

    [HttpPost(""booking"")]
    public async Task<ActionResult<ReservationDto>> CreateBooking(CreateBookingRequestDto dto)
    {
        Guest? guest = null;
        
        if (!string.IsNullOrEmpty(dto.DocNumber))
        {
            guest = await _guestRepository.GetByDocumentAsync(dto.DocNumber);
        }

        if (guest == null)
        {
            guest = new Guest
            {
                FirstName = dto.GuestName ?? ""Huésped"",
                LastName = """",
                Email = dto.GuestEmail ?? """",
                Phone = dto.GuestPhone ?? """",
                DocumentType = Enum.TryParse<IdType>(dto.DocType, out var dt) ? dt : IdType.CC,
                DocumentNumber = dto.DocNumber ?? ""SN"",
                Nationality = ""Colombia"",
                CreatedAt = DateTimeOffset.UtcNow
            };
            await _guestRepository.AddAsync(guest);
        }

        var reservation = new Reservation
        {
            MainGuestId = guest.Id,
            RoomId = dto.RoomId,
            StartDate = dto.CheckIn,
            EndDate = dto.CheckOut,
            Status = ReservationStatus.Pending,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        await _repository.CreateAsync(reservation);

        await _notificationRepository.AddAsync(
            ""Nueva Reserva Web"", 
            $""Reserva {reservation.Code} creada para {guest.FullName}"", 
            NotificationType.Success,
            $""/reservas/{reservation.Id}""
        );

        return CreatedAtAction(nameof(GetById), new { id = reservation.Id }, new { id = reservation.Id, code = reservation.Code });
    }

    [HttpPost(""{id}/checkout"")]
    public async Task<IActionResult> CheckOut(Guid id)
    {
        var reservation = await _repository.GetByIdAsync(id);
        if (reservation == null) return NotFound(""Reserva no encontrada"");

        if (reservation.Status == ReservationStatus.CheckedOut)
            return BadRequest(""La reserva ya hizo Check-out."");

        var folio = await _folioRepository.GetByReservationIdAsync(id);
        if (folio == null) return BadRequest(""Error crítico: Reserva sin folio."");
        
        if (folio.Balance > 0)
        {
            return BadRequest(new { 
                error = ""DeudaPendiente"", 
                message = $""No se puede realizar Check-out. El huésped debe $ {folio.Balance:N0}"" 
            });
        }

        var room = await _roomRepository.GetByIdAsync(reservation.RoomId);
        if (room == null) return BadRequest(""Habitación no encontrada"");

        await _repository.ProcessCheckOutAsync(reservation, room, folio);
        
        await _notificationRepository.AddAsync(
            ""Salida Confirmada"", 
            $""La habitación {room.Number} está libre y requiere limpieza."", 
            NotificationType.Warning, 
            $""/habitaciones""
        );
        
        return Ok(new { message = ""Check-out exitoso."", newStatus = ""CheckedOut"" });
    }
}
"@
Escribir-Archivo -Path (Join-Path $baseDir "API/Controllers/ReservationsController.cs") -Content $resCtrlContent

Write-Host "🎉 TODO LISTO. Ejecuta 'dotnet build' para comprobar." -ForegroundColor Cyan