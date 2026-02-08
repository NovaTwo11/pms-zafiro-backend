$SolutionName = "PmsZafiro"
$BaseDir = Get-Location

Write-Host "Generando Módulo de Habitaciones (Rooms)..." -ForegroundColor Cyan

# --- CARPETAS ---
$DtoPath = "$BaseDir/src/Application/DTOs/Rooms"
$InterfacePath = "$BaseDir/src/Application/Interfaces"
$RepoPath = "$BaseDir/src/Infrastructure/Repositories"
$ControllerPath = "$BaseDir/src/API/Controllers"

New-Item -ItemType Directory -Force -Path $DtoPath | Out-Null

# --- DTOs ---
$ContentRoomDto = @"
using $SolutionName.Domain.Enums;

namespace $SolutionName.Application.DTOs.Rooms;

public class RoomDto
{
    public Guid Id { get; set; }
    public string Number { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public decimal BasePrice { get; set; }
    public string Status { get; set; } = string.Empty;
}
"@
Set-Content -Path "$DtoPath/RoomDto.cs" -Value $ContentRoomDto

$ContentCreateRoomDto = @"
using System.ComponentModel.DataAnnotations;
using $SolutionName.Domain.Enums;

namespace $SolutionName.Application.DTOs.Rooms;

public class CreateRoomDto
{
    [Required] public string Number { get; set; } = string.Empty;
    [Required] public string Category { get; set; } = "Standard";
    [Required] public decimal BasePrice { get; set; }
    public RoomStatus Status { get; set; } = RoomStatus.Available;
}
"@
Set-Content -Path "$DtoPath/CreateRoomDto.cs" -Value $ContentCreateRoomDto

# --- INTERFAZ ---
$ContentIRepo = @"
using $SolutionName.Domain.Entities;
using $SolutionName.Domain.Enums;

namespace $SolutionName.Application.Interfaces;

public interface IRoomRepository
{
    Task<IEnumerable<Room>> GetAllAsync();
    Task<IEnumerable<Room>> GetByStatusAsync(RoomStatus status);
    Task<Room?> GetByIdAsync(Guid id);
    Task<Room> AddAsync(Room room);
    Task UpdateAsync(Room room);
    Task DeleteAsync(Guid id);
}
"@
Set-Content -Path "$InterfacePath/IRoomRepository.cs" -Value $ContentIRepo

# --- REPOSITORIO ---
$ContentRepo = @"
using Microsoft.EntityFrameworkCore;
using $SolutionName.Application.Interfaces;
using $SolutionName.Domain.Entities;
using $SolutionName.Domain.Enums;
using $SolutionName.Infrastructure.Persistence;

namespace $SolutionName.Infrastructure.Repositories;

public class RoomRepository : IRoomRepository
{
    private readonly PmsDbContext _context;

    public RoomRepository(PmsDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<Room>> GetAllAsync()
    {
        return await _context.Rooms.OrderBy(r => r.Number).ToListAsync();
    }
    
    public async Task<IEnumerable<Room>> GetByStatusAsync(RoomStatus status)
    {
        return await _context.Rooms
            .Where(r => r.Status == status)
            .OrderBy(r => r.Number)
            .ToListAsync();
    }

    public async Task<Room?> GetByIdAsync(Guid id)
    {
        return await _context.Rooms.FindAsync(id);
    }

    public async Task<Room> AddAsync(Room room)
    {
        _context.Rooms.Add(room);
        await _context.SaveChangesAsync();
        return room;
    }

    public async Task UpdateAsync(Room room)
    {
        _context.Entry(room).State = EntityState.Modified;
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(Guid id)
    {
        var room = await _context.Rooms.FindAsync(id);
        if (room != null)
        {
            _context.Rooms.Remove(room);
            await _context.SaveChangesAsync();
        }
    }
}
"@
Set-Content -Path "$RepoPath/RoomRepository.cs" -Value $ContentRepo

# --- CONTROLLER ---
$ContentController = @"
using Microsoft.AspNetCore.Mvc;
using $SolutionName.Application.DTOs.Rooms;
using $SolutionName.Application.Interfaces;
using $SolutionName.Domain.Entities;
using $SolutionName.Domain.Enums;

namespace $SolutionName.API.Controllers;

[ApiController]
[Route(""api/[controller]"")]
public class RoomsController : ControllerBase
{
    private readonly IRoomRepository _repository;

    public RoomsController(IRoomRepository repository)
    {
        _repository = repository;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<RoomDto>>> GetAll()
    {
        var rooms = await _repository.GetAllAsync();
        var dtos = rooms.Select(r => new RoomDto
        {
            Id = r.Id,
            Number = r.Number,
            Category = r.Category,
            BasePrice = r.BasePrice,
            Status = r.Status.ToString()
        });
        return Ok(dtos);
    }
    
    [HttpGet(""status/{status}"")]
    public async Task<ActionResult<IEnumerable<RoomDto>>> GetByStatus(RoomStatus status)
    {
        var rooms = await _repository.GetByStatusAsync(status);
        var dtos = rooms.Select(r => new RoomDto
        {
            Id = r.Id,
            Number = r.Number,
            Category = r.Category,
            BasePrice = r.BasePrice,
            Status = r.Status.ToString()
        });
        return Ok(dtos);
    }

    [HttpPost]
    public async Task<ActionResult<RoomDto>> Create(CreateRoomDto dto)
    {
        var room = new Room
        {
            Number = dto.Number,
            Category = dto.Category,
            BasePrice = dto.BasePrice,
            Status = dto.Status
        };

        await _repository.AddAsync(room);
        return CreatedAtAction(nameof(GetAll), new { id = room.Id }, room);
    }
    
    // Endpoint rápido para cambiar estado (Limpieza/CheckIn)
    [HttpPatch(""{id}/status"")]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] RoomStatus newStatus)
    {
        var room = await _repository.GetByIdAsync(id);
        if (room == null) return NotFound();
        
        room.Status = newStatus;
        await _repository.UpdateAsync(room);
        
        return NoContent();
    }
}
"@
Set-Content -Path "$ControllerPath/RoomsController.cs" -Value $ContentController

Write-Host "¡Módulo de Habitaciones generado!" -ForegroundColor Green