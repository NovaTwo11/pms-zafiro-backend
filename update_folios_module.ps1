# Script para actualizar Módulo de Folios y Reservas en PmsZafiro

$basePath = "src"

# 1. Crear CreateExternalFolioDto.cs
$dtoContent = @"
namespace PmsZafiro.Application.DTOs.Folios;

public class CreateExternalFolioDto
{
    public string Alias { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}
"@
Set-Content -Path "$basePath/Application/DTOs/Folios/CreateExternalFolioDto.cs" -Value $dtoContent -Force
Write-Host "Creado CreateExternalFolioDto.cs"

# 2. Actualizar IFolioRepository.cs
$interfaceContent = @"
using PmsZafiro.Domain.Entities;

namespace PmsZafiro.Application.Interfaces;

public interface IFolioRepository
{
    Task<Folio?> GetByIdAsync(Guid id);
    Task<GuestFolio?> GetByReservationIdAsync(Guid reservationId);
    Task CreateAsync(Folio folio);
    Task AddTransactionAsync(FolioTransaction transaction);
    Task UpdateAsync(Folio folio); // Para actualizar estado
    
    // Nuevos métodos para listas
    Task<IEnumerable<GuestFolio>> GetActiveGuestFoliosAsync();
    Task<IEnumerable<ExternalFolio>> GetActiveExternalFoliosAsync();
}
"@
Set-Content -Path "$basePath/Application/Interfaces/IFolioRepository.cs" -Value $interfaceContent -Force
Write-Host "Actualizado IFolioRepository.cs"

# 3. Actualizar FolioRepository.cs (Implementación)
$repoContent = @"
using Microsoft.EntityFrameworkCore;
using PmsZafiro.Application.Interfaces;
using PmsZafiro.Domain.Entities;
using PmsZafiro.Domain.Enums;
using PmsZafiro.Infrastructure.Persistence;

namespace PmsZafiro.Infrastructure.Repositories;

public class FolioRepository : IFolioRepository
{
    private readonly PmsDbContext _context;

    public FolioRepository(PmsDbContext context)
    {
        _context = context;
    }

    public async Task<Folio?> GetByIdAsync(Guid id)
    {
        return await _context.Folios
            .Include(f => f.Transactions)
            .FirstOrDefaultAsync(f => f.Id == id);
    }

    public async Task<GuestFolio?> GetByReservationIdAsync(Guid reservationId)
    {
        return await _context.Folios
            .OfType<GuestFolio>()
            .Include(f => f.Transactions)
            .Include(f => f.Reservation) // Incluir datos de la reserva si es necesario
            .FirstOrDefaultAsync(f => f.ReservationId == reservationId);
    }

    public async Task CreateAsync(Folio folio)
    {
        await _context.Folios.AddAsync(folio);
        await _context.SaveChangesAsync();
    }

    public async Task AddTransactionAsync(FolioTransaction transaction)
    {
        await _context.Set<FolioTransaction>().AddAsync(transaction);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(Folio folio)
    {
        _context.Folios.Update(folio);
        await _context.SaveChangesAsync();
    }

    public async Task<IEnumerable<GuestFolio>> GetActiveGuestFoliosAsync()
    {
        return await _context.Folios
            .OfType<GuestFolio>()
            .Include(f => f.Transactions)
            .Include(f => f.Reservation)
            .ThenInclude(r => r.MainGuest)
            .Include(f => f.Reservation)
            .ThenInclude(r => r.Room)
            .Where(f => f.Status == FolioStatus.Open)
            .ToListAsync();
    }

    public async Task<IEnumerable<ExternalFolio>> GetActiveExternalFoliosAsync()
    {
        return await _context.Folios
            .OfType<ExternalFolio>()
            .Include(f => f.Transactions)
            .Where(f => f.Status == FolioStatus.Open)
            .ToListAsync();
    }
}
"@
Set-Content -Path "$basePath/Infrastructure/Repositories/FolioRepository.cs" -Value $repoContent -Force
Write-Host "Actualizado FolioRepository.cs"

# 4. Actualizar FoliosController.cs
$foliosControllerContent = @"
using Microsoft.AspNetCore.Mvc;
using PmsZafiro.Application.DTOs.Folios;
using PmsZafiro.Application.Interfaces;
using PmsZafiro.Domain.Entities;
using PmsZafiro.Domain.Enums;

namespace PmsZafiro.API.Controllers;

[ApiController]
[Route(""api/[controller]"")]
public class FoliosController : ControllerBase
{
    private readonly IFolioRepository _repository;

    public FoliosController(IFolioRepository repository)
    {
        _repository = repository;
    }

    [HttpGet(""active-guests"")]
    public async Task<ActionResult<IEnumerable<object>>> GetActiveGuests()
    {
        var folios = await _repository.GetActiveGuestFoliosAsync();
        // Mapeo manual simple para la lista
        var result = folios.Select(f => {
            var charges = f.Transactions.Where(t => t.Type == TransactionType.Charge).Sum(t => t.Amount);
            var payments = f.Transactions.Where(t => t.Type == TransactionType.Payment).Sum(t => t.Amount);
            return new 
            {
                Id = f.Id,
                Status = f.Status.ToString(),
                Balance = charges - payments,
                GuestName = f.Reservation.MainGuest?.FullName ?? ""Desconocido"",
                RoomNumber = f.Reservation.Room?.Number ?? ""?"",
                CheckIn = f.Reservation.StartDate,
                CheckOut = f.Reservation.EndDate,
                Nights = f.Reservation.Nights
            };
        });
        return Ok(result);
    }

    [HttpGet(""active-externals"")]
    public async Task<ActionResult<IEnumerable<object>>> GetActiveExternals()
    {
        var folios = await _repository.GetActiveExternalFoliosAsync();
        var result = folios.Select(f => {
            var charges = f.Transactions.Where(t => t.Type == TransactionType.Charge).Sum(t => t.Amount);
            var payments = f.Transactions.Where(t => t.Type == TransactionType.Payment).Sum(t => t.Amount);
            return new 
            {
                Id = f.Id,
                Status = f.Status.ToString(),
                Balance = charges - payments,
                Alias = f.Alias,
                Description = f.Description,
                CreatedAt = DateTime.Now // Ajustar si tienes fecha de creación en Folio base
            };
        });
        return Ok(result);
    }

    [HttpPost(""external"")]
    public async Task<IActionResult> CreateExternal([FromBody] CreateExternalFolioDto dto)
    {
        var folio = new ExternalFolio
        {
            Alias = dto.Alias,
            Description = dto.Description,
            Status = FolioStatus.Open
        };
        
        await _repository.CreateAsync(folio);
        return Ok(new { id = folio.Id, message = ""Folio externo creado"" });
    }

    [HttpGet(""reservation/{reservationId}"")]
    public async Task<ActionResult<FolioDto>> GetByReservation(Guid reservationId)
    {
        var folio = await _repository.GetByReservationIdAsync(reservationId);
        if (folio == null) return NotFound(""No se encontró folio para esta reserva"");

        return Ok(MapToDto(folio));
    }

    [HttpGet(""{id}"")]
    public async Task<ActionResult<FolioDto>> GetById(Guid id)
    {
        var folio = await _repository.GetByIdAsync(id);
        if (folio == null) return NotFound();

        return Ok(MapToDto(folio));
    }

    [HttpPost(""{id}/transactions"")]
    public async Task<IActionResult> AddTransaction(Guid id, [FromBody] CreateTransactionDto dto)
    {
        var folio = await _repository.GetByIdAsync(id);
        if (folio == null) return NotFound();

        var transaction = new FolioTransaction
        {
            FolioId = id,
            Amount = dto.Amount,
            Description = dto.Description,
            Type = dto.Type, // Enum string conversion handleado por JSON
            Quantity = dto.Quantity,
            UnitPrice = dto.UnitPrice > 0 ? dto.UnitPrice : dto.Amount,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedByUserId = ""Admin"" 
        };

        await _repository.AddTransactionAsync(transaction);

        return Ok(new { message = ""Transacción agregada"", transactionId = transaction.Id });
    }

    private FolioDto MapToDto(Folio folio)
    {
        var charges = folio.Transactions.Where(t => t.Type == TransactionType.Charge).Sum(t => t.Amount);
        var payments = folio.Transactions.Where(t => t.Type == TransactionType.Payment).Sum(t => t.Amount);
        
        return new FolioDto
        {
            Id = folio.Id,
            ReservationId = (folio as GuestFolio)?.ReservationId,
            Status = folio.Status.ToString(),
            Balance = charges - payments,
            TotalCharges = charges,
            TotalPayments = payments,
            Transactions = folio.Transactions.OrderByDescending(t => t.CreatedAt).Select(t => new FolioTransactionDto
            {
                Id = t.Id,
                Date = t.CreatedAt.ToString(""yyyy-MM-dd HH:mm""),
                Description = t.Description,
                Amount = t.Amount,
                Type = t.Type.ToString(),
                UnitPrice = t.UnitPrice,
                Quantity = t.Quantity,
                User = t.CreatedByUserId
            }).ToList()
        };
    }
}
"@
Set-Content -Path "$basePath/API/Controllers/FoliosController.cs" -Value $foliosControllerContent -Force
Write-Host "Actualizado FoliosController.cs"

# 5. Actualizar ReservationsController.cs (Añadir CheckIn y creación de Folio)
$reservationsControllerContent = @"
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

    // NUEVO ENDPOINT: CHECK-IN
    [HttpPost(""{id}/checkin"")]
    public async Task<IActionResult> CheckIn(Guid id)
    {
        var reservation = await _repository.GetByIdAsync(id);
        if (reservation == null) return NotFound(""Reserva no encontrada"");

        if (reservation.Status != ReservationStatus.Pending && reservation.Status != ReservationStatus.Confirmed)
            return BadRequest(""El estado de la reserva no permite Check-in."");

        // 1. Actualizar estado de Reserva
        reservation.Status = ReservationStatus.CheckedIn;
        await _repository.UpdateAsync(reservation); // Asumiendo que existe UpdateAsync en repo

        // 2. Actualizar estado de Habitación
        var room = await _roomRepository.GetByIdAsync(reservation.RoomId);
        if (room != null)
        {
            room.Status = RoomStatus.Occupied;
            await _roomRepository.UpdateAsync(room);
        }

        // 3. Crear Folio del Huésped automáticamente
        var existingFolio = await _folioRepository.GetByReservationIdAsync(id);
        if (existingFolio == null)
        {
            var folio = new GuestFolio
            {
                ReservationId = id,
                Status = FolioStatus.Open
            };
            await _folioRepository.CreateAsync(folio);
        }

        await _notificationRepository.AddAsync(
            ""Check-in Realizado"", 
            $""Huésped {reservation.MainGuest?.FullName} ingresó a habitación {room?.Number}"", 
            NotificationType.Info, 
            $""/folios""
        );

        return Ok(new { message = ""Check-in exitoso y Folio creado."", status = ""CheckedIn"" });
    }

    [HttpPost(""{id}/checkout"")]
    public async Task<IActionResult> CheckOut(Guid id)
    {
        var reservation = await _repository.GetByIdAsync(id);
        if (reservation == null) return NotFound(""Reserva no encontrada"");

        if (reservation.Status == ReservationStatus.CheckedOut)
            return BadRequest(""La reserva ya hizo Check-out."");

        var folio = await _folioRepository.GetByReservationIdAsync(id);
        
        // Validación de deuda: No permitir checkout si hay saldo > 0
        if (folio != null && folio.Balance > 0)
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
Set-Content -Path "$basePath/API/Controllers/ReservationsController.cs" -Value $reservationsControllerContent -Force
Write-Host "Actualizado ReservationsController.cs con endpoint CheckIn"

Write-Host "Proceso completado."