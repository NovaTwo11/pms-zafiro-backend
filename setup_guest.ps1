$SolutionName = "PmsZafiro"
$BaseDir = Get-Location

Write-Host "Generando Módulo de Huéspedes (Guests)..." -ForegroundColor Cyan

# --- 1. CAPA APPLICATION (DTOs e Interfaces) ---
$DtoPath = "$BaseDir/src/Application/DTOs/Guests"
$InterfacePath = "$BaseDir/src/Application/Interfaces"
New-Item -ItemType Directory -Force -Path $DtoPath | Out-Null
New-Item -ItemType Directory -Force -Path $InterfacePath | Out-Null

# DTO para LEER (Salida)
$ContentGuestDto = @"
using $SolutionName.Domain.Enums;

namespace $SolutionName.Application.DTOs.Guests;

public class GuestDto
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string DocumentType { get; set; } = string.Empty;
    public string DocumentNumber { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Nationality { get; set; } = string.Empty;
}
"@
Set-Content -Path "$DtoPath/GuestDto.cs" -Value $ContentGuestDto

# DTO para CREAR (Entrada)
$ContentCreateGuestDto = @"
using $SolutionName.Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace $SolutionName.Application.DTOs.Guests;

public class CreateGuestDto
{
    [Required] public string FirstName { get; set; } = string.Empty;
    [Required] public string LastName { get; set; } = string.Empty;
    [Required] public IdType DocumentType { get; set; }
    [Required] public string DocumentNumber { get; set; } = string.Empty;
    [EmailAddress] public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Nationality { get; set; } = string.Empty;
    public DateOnly? BirthDate { get; set; }
}
"@
Set-Content -Path "$DtoPath/CreateGuestDto.cs" -Value $ContentCreateGuestDto

# Interfaz del Repositorio
$ContentIRepo = @"
using $SolutionName.Domain.Entities;

namespace $SolutionName.Application.Interfaces;

public interface IGuestRepository
{
    Task<IEnumerable<Guest>> GetAllAsync();
    Task<Guest?> GetByIdAsync(Guid id);
    Task<Guest> AddAsync(Guest guest);
    Task UpdateAsync(Guest guest);
    Task DeleteAsync(Guid id);
}
"@
Set-Content -Path "$InterfacePath/IGuestRepository.cs" -Value $ContentIRepo

# --- 2. CAPA INFRASTRUCTURE (Implementación) ---
$RepoPath = "$BaseDir/src/Infrastructure/Repositories"
New-Item -ItemType Directory -Force -Path $RepoPath | Out-Null

$ContentRepo = @"
using Microsoft.EntityFrameworkCore;
using $SolutionName.Application.Interfaces;
using $SolutionName.Domain.Entities;
using $SolutionName.Infrastructure.Persistence;

namespace $SolutionName.Infrastructure.Repositories;

public class GuestRepository : IGuestRepository
{
    private readonly PmsDbContext _context;

    public GuestRepository(PmsDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<Guest>> GetAllAsync()
    {
        return await _context.Guests.OrderByDescending(g => g.CreatedAt).ToListAsync();
    }

    public async Task<Guest?> GetByIdAsync(Guid id)
    {
        return await _context.Guests.FindAsync(id);
    }

    public async Task<Guest> AddAsync(Guest guest)
    {
        _context.Guests.Add(guest);
        await _context.SaveChangesAsync();
        return guest;
    }

    public async Task UpdateAsync(Guest guest)
    {
        _context.Entry(guest).State = EntityState.Modified;
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
Set-Content -Path "$RepoPath/GuestRepository.cs" -Value $ContentRepo

# --- 3. CAPA API (Controller) ---
$ControllerPath = "$BaseDir/src/API/Controllers"
New-Item -ItemType Directory -Force -Path $ControllerPath | Out-Null

$ContentController = @"
using Microsoft.AspNetCore.Mvc;
using $SolutionName.Application.DTOs.Guests;
using $SolutionName.Application.Interfaces;
using $SolutionName.Domain.Entities;

namespace $SolutionName.API.Controllers;

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
        var guests = await _repository.GetAllAsync();
        
        // Mapeo manual simple (luego usaremos AutoMapper)
        var dtos = guests.Select(g => new GuestDto
        {
            Id = g.Id,
            FullName = g.FullName,
            DocumentType = g.DocumentType.ToString(),
            DocumentNumber = g.DocumentNumber,
            Email = g.Email,
            Phone = g.Phone,
            Nationality = g.Nationality
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
Set-Content -Path "$ControllerPath/GuestsController.cs" -Value $ContentController

Write-Host "¡Módulo de Huéspedes generado!" -ForegroundColor Green