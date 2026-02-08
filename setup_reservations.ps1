$SolutionName = "PmsZafiro"
$BaseDir = Get-Location

Write-Host "Generando Módulo de Reservas (Reservations)..." -ForegroundColor Cyan

# --- CARPETAS ---
$DtoPath = "$BaseDir/src/Application/DTOs/Reservations"
$InterfacePath = "$BaseDir/src/Application/Interfaces"
$RepoPath = "$BaseDir/src/Infrastructure/Repositories"
$ControllerPath = "$BaseDir/src/API/Controllers"

New-Item -ItemType Directory -Force -Path $DtoPath | Out-Null

# --- DTOs ---
# DTO de Salida (Lo que ve el usuario)
$ContentResDto = @"
using $SolutionName.Domain.Enums;

namespace $SolutionName.Application.DTOs.Reservations;

public class ReservationDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    
    // Datos aplanados para facilitar el frontend
    public Guid MainGuestId { get; set; }
    public string MainGuestName { get; set; } = string.Empty;
    
    public Guid RoomId { get; set; }
    public string RoomNumber { get; set; } = string.Empty;
    
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public int Nights { get; set; }
    
    // Para saber si ya tiene folio (casi siempre sí)
    public bool HasFolio { get; set; }
}
"@
Set-Content -Path "$DtoPath/ReservationDto.cs" -Value $ContentResDto

# DTO de Entrada (Crear)
$ContentCreateResDto = @"
using System.ComponentModel.DataAnnotations;

namespace $SolutionName.Application.DTOs.Reservations;

public class CreateReservationDto
{
    [Required] public Guid MainGuestId { get; set; }
    [Required] public Guid RoomId { get; set; }
    [Required] public DateOnly StartDate { get; set; }
    [Required] public DateOnly EndDate { get; set; }
}
"@
Set-Content -Path "$DtoPath/CreateReservationDto.cs" -Value $ContentCreateResDto

# --- INTERFAZ ---
$ContentIRepo = @"
using $SolutionName.Domain.Entities;

namespace $SolutionName.Application.Interfaces;

public interface IReservationRepository
{
    Task<IEnumerable<Reservation>> GetAllAsync();
    Task<Reservation?> GetByIdAsync(Guid id);
    Task<Reservation> CreateAsync(Reservation reservation);
    Task UpdateAsync(Reservation reservation);
    // Método útil para buscar por código humano (RES-XXX)
    Task<Reservation?> GetByCodeAsync(string code);
}
"@
Set-Content -Path "$InterfacePath/IReservationRepository.cs" -Value $ContentIRepo

# --- REPOSITORIO (LÓGICA DE NEGOCIO POTENTE) ---
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
                .ThenInclude(rg => rg.Guest)
            .FirstOrDefaultAsync(r => r.Id == id);
    }
    
    public async Task<Reservation?> GetByCodeAsync(string code)
    {
        return await _context.Reservations
            .Include(r => r.MainGuest)
            .FirstOrDefaultAsync(r => r.Code == code);
    }

    // AQUÍ OCURRE LA MAGIA
    public async Task<Reservation> CreateAsync(Reservation reservation)
    {
        // 1. Asignar un código legible si no tiene
        if (string.IsNullOrEmpty(reservation.Code))
        {
            reservation.Code = ""RES-"" + new Random().Next(1000, 9999);
        }

        // 2. Guardar la Reserva
        _context.Reservations.Add(reservation);
        
        // 3. Crear automáticamente el vínculo en la tabla de detalles (Primary Guest)
        // Esto evita errores de integridad referencial luego
        var detail = new ReservationGuestDetail
        {
            ReservationId = reservation.Id,
            GuestId = reservation.MainGuestId,
            IsPrimary = true,
            OriginCity = ""Desconocida"", // Se puede actualizar luego en Check-in
            OriginCountry = ""Desconocida""
        };
        _context.ReservationGuests.Add(detail);

        // 4. Crear automáticamente el FOLIO (Cuenta) del huésped
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
}
"@
Set-Content -Path "$RepoPath/ReservationRepository.cs" -Value $ContentRepo

# --- CONTROLLER ---
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

    public ReservationsController(IReservationRepository repository)
    {
        _repository = repository;
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
            HasFolio = true // Siempre creamos folio al crear reserva
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
        // Nota: En un sistema real aquí validaríamos disponibilidad de fechas
        
        var reservation = new Reservation
        {
            MainGuestId = dto.MainGuestId,
            RoomId = dto.RoomId,
            StartDate = dto.StartDate,
            EndDate = dto.EndDate,
            Status = ReservationStatus.Pending
        };

        await _repository.CreateAsync(reservation);

        // Retornamos 201 Created
        return CreatedAtAction(nameof(GetById), new { id = reservation.Id }, new { id = reservation.Id, code = reservation.Code });
    }
}
"@
Set-Content -Path "$ControllerPath/ReservationsController.cs" -Value $ContentController

Write-Host "¡Módulo de Reservas generado!" -ForegroundColor Green