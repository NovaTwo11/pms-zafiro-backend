using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PmsZafiro.Application.DTOs.Channels;
using PmsZafiro.Domain.Entities;
using PmsZafiro.Domain.Enums;
using PmsZafiro.Infrastructure.Persistence;

namespace PmsZafiro.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ChannelsController : ControllerBase
{
    private readonly PmsDbContext _context;

    public ChannelsController(PmsDbContext context)
    {
        _context = context;
    }

    // GET: api/channels/mappings?channel=BookingCom
    [HttpGet("mappings")]
    public async Task<IActionResult> GetMappings([FromQuery] BookingChannel channel)
    {
        var mappings = await _context.Set<ChannelRoomMapping>()
            .Where(m => m.Channel == channel && m.IsActive)
            .Select(m => new ChannelMappingDto
            {
                RoomCategory = m.RoomCategory,
                ExternalRoomId = m.ExternalRoomId,
                Channel = m.Channel
            })
            .ToListAsync();

        return Ok(mappings);
    }

    // POST: api/channels/mappings
    [HttpPost("mappings")]
    public async Task<IActionResult> SaveMapping([FromBody] ChannelMappingDto dto)
    {
        // Buscar si ya existe un mapeo para esa categoría y canal
        var existing = await _context.Set<ChannelRoomMapping>()
            .FirstOrDefaultAsync(m => m.RoomCategory == dto.RoomCategory && m.Channel == dto.Channel);

        if (existing != null)
        {
            // Actualizar
            existing.ExternalRoomId = dto.ExternalRoomId;
            existing.CreatedAt = DateTimeOffset.UtcNow; // Refrescar timestamp
            _context.Set<ChannelRoomMapping>().Update(existing);
        }
        else
        {
            // Crear nuevo
            var newMapping = new ChannelRoomMapping
            {
                Channel = dto.Channel,
                RoomCategory = dto.RoomCategory,
                ExternalRoomId = dto.ExternalRoomId,
                IsActive = true
            };
            await _context.Set<ChannelRoomMapping>().AddAsync(newMapping);
        }

        await _context.SaveChangesAsync();
        return Ok(new { message = "Mapeo guardado exitosamente" });
    }
    
    // GET: api/channels/room-categories
    // Endpoint auxiliar para llenar el Select en el Frontend con las categorías reales de la BD
    [HttpGet("room-categories")]
    public async Task<IActionResult> GetRoomCategories()
    {
        // Obtener categorías únicas de las habitaciones existentes
        var categories = await _context.Rooms
            .Select(r => r.Category)
            .Distinct()
            .ToListAsync();
            
        return Ok(categories);
    }
}