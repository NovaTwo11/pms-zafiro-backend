$SolutionName = "PmsZafiro"
$BaseDir = Get-Location

Write-Host "Implementando Lógica de Check-Out y Validaciones..." -ForegroundColor Cyan

# 1. Actualizar INTERFAZ del Repositorio
$InterfaceFile = "$BaseDir/src/Application/Interfaces/IReservationRepository.cs"
$ContentInterface = @"
using $SolutionName.Domain.Entities;

namespace $SolutionName.Application.Interfaces;

public interface IReservationRepository
{
    Task<IEnumerable<Reservation>> GetAllAsync();
    Task<Reservation?> GetByIdAsync(Guid id);
    Task<Reservation> CreateAsync(Reservation reservation);
    Task UpdateAsync(Reservation reservation);
    Task<Reservation?> GetByCodeAsync(string code);
    
    // NUEVO: Método transaccional para el Check-Out
    Task ProcessCheckOutAsync(Reservation reservation, Room room, Folio folio);
}
"@
Set-Content -Path $InterfaceFile -Value $ContentInterface

# 2. Actualizar REPOSITORIO (Implementación de la lógica)
$RepoFile = "$BaseDir/src/Infrastructure/Repositories/ReservationRepository.cs"
$ContentRepo = @"
using Microsoft.EntityFrameworkCore;
using $SolutionName.Application.Interfaces;
using $SolutionName.Domain.Entities;
using $SolutionName.Domain.Enums;
using $SolutionName.Infrastructure.Persistence;

namespace $SolutionName.Infrastructure.Repositories;

public class ReservationRepository : IReservationRepository
{
    private readonly PmsDbContext _context;

    public ReservationRepository(PmsDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<Reservation>> GetAllAsync()
    {
        return await _context.Reservations
            .Include(r => r.MainGuest)
            .Include(r => r.Room)
            .OrderByDescending(r => r.StartDate)
            .ToListAsync();
    }

    public async Task<Reservation?> GetByIdAsync(Guid id)
    {
        return await _context.Reservations
            .Include(r => r.MainGuest)
            .Include(r => r.Room)
            .Include(r => r.Guests)
            .FirstOrDefaultAsync(r => r.Id == id);
    }
    
    public async Task<Reservation?> GetByCodeAsync(string code)
    {
        return await _context.Reservations
            .Include(r => r.MainGuest)
            .FirstOrDefaultAsync(r => r.Code == code);
    }

    public async Task<Reservation> CreateAsync(Reservation reservation)
    {
        if (string.IsNullOrEmpty(reservation.Code))
            reservation.Code = ""RES-"" + new Random().Next(1000, 9999);

        _context.Reservations.Add(reservation);
        
        var detail = new ReservationGuestDetail
        {
            ReservationId = reservation.Id,
            GuestId = reservation.MainGuestId,
            IsPrimary = true,
            OriginCity = ""Desconocida"",
            OriginCountry = ""Desconocida""
        };
        _context.ReservationGuests.Add(detail);

        var folio = new GuestFolio
        {
            Id = Guid.NewGuid(),
            ReservationId = reservation.Id,
            Status = FolioStatus.Open
        };
        _context.Folios.Add(folio);

        await _context.SaveChangesAsync();
        return reservation;
    }

    public async Task UpdateAsync(Reservation reservation)
    {
        _context.Entry(reservation).State = EntityState.Modified;
        await _context.SaveChangesAsync();
    }

    // --- LÓGICA DE CHECK-OUT ---
    public async Task ProcessCheckOutAsync(Reservation reservation, Room room, Folio folio)
    {
        // 1. Cerrar Reserva
        reservation.Status = ReservationStatus.CheckedOut;
        reservation.UpdatedAt = DateTimeOffset.UtcNow;
        _context.Entry(reservation).State = EntityState.Modified;

        // 2. Marcar Habitación como Sucia
        room.Status = RoomStatus.Dirty;
        _context.Entry(room).State = EntityState.Modified;

        // 3. Cerrar Folio
        folio.Status = FolioStatus.Closed;
        _context.Entry(folio).State = EntityState.Modified;

        // Todo se guarda en una sola transacción atómica
        await _context.SaveChangesAsync();
    }
}
"@
Set-Content -Path $RepoFile -Value $ContentRepo

# 3. Actualizar CONTROLADOR (Endpoint)
$ControllerFile = "$BaseDir/src/API/Controllers/ReservationsController.cs"
$ContentController = @"
using Microsoft.AspNetCore.Mvc;
using $SolutionName.Application.DTOs.Reservations;
using $SolutionName.Application.Interfaces;
using $SolutionName.Domain.Entities;
using $SolutionName.Domain.Enums;

namespace $SolutionName.API.Controllers;

[ApiController]
[Route(""api/[controller]"")]
public class ReservationsController : ControllerBase
{
    private readonly IReservationRepository _repository;
    private readonly IFolioRepository _folioRepository; // Inyectamos Folio
    private readonly IRoomRepository _roomRepository;   // Inyectamos Room

    public ReservationsController(
        IReservationRepository repository,
        IFolioRepository folioRepository,
        IRoomRepository roomRepository)
    {
        _repository = repository;
        _folioRepository = folioRepository;
        _roomRepository = roomRepository;
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
            MainGuestName = r.MainGuest.FullName,
            RoomId = r.RoomId,
            RoomNumber = r.Room.Number,
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
            Status = ReservationStatus.Pending
        };

        await _repository.CreateAsync(reservation);

        return CreatedAtAction(nameof(GetById), new { id = reservation.Id }, new { id = reservation.Id, code = reservation.Code });
    }

    // --- NUEVO ENDPOINT: CHECK-OUT ---
    [HttpPost(""{id}/checkout"")]
    public async Task<IActionResult> CheckOut(Guid id)
    {
        // 1. Buscar Reserva
        var reservation = await _repository.GetByIdAsync(id);
        if (reservation == null) return NotFound(""Reserva no encontrada"");

        if (reservation.Status == ReservationStatus.CheckedOut)
            return BadRequest(""La reserva ya hizo Check-out."");

        // 2. Buscar Folio y Validar Deuda
        var folio = await _folioRepository.GetByReservationIdAsync(id);
        if (folio == null) return BadRequest(""Error crítico: Reserva sin folio."");
        
        // Calculamos saldo al vuelo
        var balance = folio.Balance; 
        
        if (balance > 0)
        {
            return BadRequest(new { 
                error = ""DeudaPendiente"", 
                message = $""No se puede realizar Check-out. El huésped debe $ {balance:N0}"" 
            });
        }

        // 3. Buscar Habitación
        var room = await _roomRepository.GetByIdAsync(reservation.RoomId);
        if (room == null) return BadRequest(""Habitación no encontrada"");

        // 4. Ejecutar Proceso
        await _repository.ProcessCheckOutAsync(reservation, room, folio);

        return Ok(new { message = ""Check-out exitoso. Habitación marcada como Sucia."", newStatus = ""CheckedOut"" });
    }
}
"@
Set-Content -Path $ControllerFile -Value $ContentController

Write-Host "¡Lógica de Check-Out implementada!" -ForegroundColor Green