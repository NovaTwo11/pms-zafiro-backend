using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PmsZafiro.Application.DTOs.Rooms;
using PmsZafiro.Application.Interfaces;
using PmsZafiro.Domain.Entities;
using PmsZafiro.Domain.Enums;
using PmsZafiro.Infrastructure.Persistence;

namespace PmsZafiro.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RoomsController : ControllerBase
{
    private readonly IRoomRepository _repository;
    private readonly PmsDbContext _dbContext;

    public RoomsController(IRoomRepository repository, PmsDbContext dbContext)
    {
        _repository = repository;
        _dbContext = dbContext;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<RoomDto>>> GetAll()
    {
        var rooms = await _dbContext.Rooms
            .Include(r => r.PriceOverrides)
            .ToListAsync();
            
        var dtos = rooms.Select(r => new RoomDto
        {
            Id = r.Id,
            Number = r.Number,
            Floor = r.Floor,
            Category = r.Category,
            BasePrice = r.BasePrice,
            Status = r.Status.ToString(),
            // Convertimos DateOnly a DateTime explícitamente para el DTO
            PriceOverrides = r.PriceOverrides?.Select(po => new RoomPriceOverrideDto 
            {
                RoomId = po.RoomId,
                Date = po.Date.ToDateTime(TimeOnly.MinValue), 
                Price = po.Price
            }).ToList() ?? new List<RoomPriceOverrideDto>()
        });
        
        return Ok(dtos);
    }
    
    [HttpGet("status/{status}")]
    public async Task<ActionResult<IEnumerable<RoomDto>>> GetByStatus(RoomStatus status)
    {
        var rooms = await _repository.GetByStatusAsync(status);
        var dtos = rooms.Select(r => new RoomDto
        {
            Id = r.Id,
            Number = r.Number,
            Floor = r.Floor,
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
            Id = Guid.NewGuid(),
            Number = dto.Number,
            Floor = dto.Floor,
            Category = dto.Category,
            BasePrice = dto.BasePrice,
            Status = dto.Status
        };

        await _repository.AddAsync(room);
        return CreatedAtAction(nameof(GetAll), new { id = room.Id }, room);
    }
    
    [HttpPatch("{id}/status")]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] RoomStatus newStatus)
    {
        var room = await _repository.GetByIdAsync(id);
        if (room == null) return NotFound();
        
        room.Status = newStatus;
        await _repository.UpdateAsync(room);
        
        return NoContent();
    }
    
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] CreateRoomDto dto)
    {
        if (id == Guid.Empty) return BadRequest();

        var existingRoom = await _repository.GetByIdAsync(id);
        if (existingRoom == null) return NotFound();

        existingRoom.Number = dto.Number;
        existingRoom.Floor = dto.Floor;
        existingRoom.Category = dto.Category;
        existingRoom.BasePrice = dto.BasePrice;

        await _repository.UpdateAsync(existingRoom);
        
        return NoContent();
    }

    [HttpPost("rates")]
    public async Task<IActionResult> SetRates([FromBody] SetRoomRateDto dto)
    {
        if (dto.StartDate.Date > dto.EndDate.Date) 
            return BadRequest(new { message = "La fecha de inicio no puede ser mayor a la fecha de fin." });

        var query = _dbContext.Rooms.AsQueryable();
        
        if (dto.RoomId.HasValue && dto.RoomId.Value != Guid.Empty)
            query = query.Where(r => r.Id == dto.RoomId.Value);
        else if (!string.IsNullOrEmpty(dto.Category))
            query = query.Where(r => r.Category == dto.Category);

        var roomsAffected = await query.ToListAsync();
        if (!roomsAffected.Any()) 
            return NotFound(new { message = "No se encontraron habitaciones con los criterios seleccionados." });

        var roomIds = roomsAffected.Select(r => r.Id).ToList();
        
        // Convertimos los DateTimes del frontend a DateOnly para LINQ
        var startDateOnly = DateOnly.FromDateTime(dto.StartDate);
        var endDateOnly = DateOnly.FromDateTime(dto.EndDate);
        
        var existingOverrides = await _dbContext.RoomPriceOverrides
            .Where(o => roomIds.Contains(o.RoomId) && o.Date >= startDateOnly && o.Date <= endDateOnly)
            .ToListAsync();
            
        _dbContext.RoomPriceOverrides.RemoveRange(existingOverrides);

        var newOverrides = new List<RoomPriceOverride>();
        foreach (var room in roomsAffected)
        {
            for (var currentDate = dto.StartDate.Date; currentDate <= dto.EndDate.Date; currentDate = currentDate.AddDays(1))
            {
                newOverrides.Add(new RoomPriceOverride
                {
                    Id = Guid.NewGuid(),
                    RoomId = room.Id,
                    Date = DateOnly.FromDateTime(currentDate), // Convertimos DateTime a DateOnly
                    Price = dto.Price
                });
            }
        }

        await _dbContext.RoomPriceOverrides.AddRangeAsync(newOverrides);
        await _dbContext.SaveChangesAsync();

        return Ok(new { message = "Tarifas actualizadas correctamente." });
    }
}