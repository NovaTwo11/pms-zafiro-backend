using Microsoft.AspNetCore.Mvc;
using PmsZafiro.Application.DTOs.Rooms;
using PmsZafiro.Application.Interfaces;
using PmsZafiro.Domain.Entities;
using PmsZafiro.Domain.Enums;

namespace PmsZafiro.API.Controllers;

[ApiController]
[Route("api/[controller]")]
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
    
    [HttpGet("status/{status}")]
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
    [HttpPatch("{id}/status")]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] RoomStatus newStatus)
    {
        var room = await _repository.GetByIdAsync(id);
        if (room == null) return NotFound();
        
        room.Status = newStatus;
        await _repository.UpdateAsync(room);
        
        return NoContent();
    }
}
