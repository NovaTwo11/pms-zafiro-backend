using Microsoft.AspNetCore.Mvc;
using PmsZafiro.Application.DTOs.Guests;
using PmsZafiro.Application.Interfaces;
using PmsZafiro.Domain.Entities;
using PmsZafiro.Domain.Enums;

namespace PmsZafiro.API.Controllers;

[ApiController]
[Route("api/[controller]")]
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
            var lastRes = g.Reservations.OrderByDescending(r => r.CheckOut).FirstOrDefault();
            var isActive = g.Reservations.Any(r => r.Status == ReservationStatus.CheckedIn);

            return new GuestDto
            {
                Id = g.Id,
                FullName = g.FullName,
                FirstName = g.FirstName,
                LastName = g.LastName,
                DocumentType = g.DocumentType.ToString(),
                DocumentNumber = g.DocumentNumber,
                Email = g.Email,
                Phone = g.Phone,
                Nationality = g.Nationality,
                TotalStays = g.Reservations.Count(r => r.Status != ReservationStatus.Cancelled),
                LastStayDate = lastRes?.CheckOut, 
                CurrentStatus = isActive ? "in-house" : "previous",
                BirthDate = g.BirthDate
            };
        });

        return Ok(dtos);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<GuestDto>> GetById(Guid id)
    {
        var guest = await _repository.GetByIdAsync(id);
        if (guest == null) return NotFound();

        return Ok(new GuestDto
        {
            Id = guest.Id,
            FullName = guest.FullName,
            FirstName = guest.FirstName,
            LastName = guest.LastName,
            DocumentType = guest.DocumentType.ToString(),
            DocumentNumber = guest.DocumentNumber,
            Email = guest.Email,
            Phone = guest.Phone,
            Nationality = guest.Nationality,
            BirthDate = guest.BirthDate
        });
    }

    [HttpPost]
    public async Task<ActionResult<GuestDto>> Create(CreateGuestDto dto)
    {
        var existing = await _repository.GetByDocumentAsync(dto.DocumentNumber);
        if (existing != null)
        {
            return Conflict(new { message = $"Ya existe un huésped con el documento {dto.DocumentNumber}" });
        }

        var guest = new Guest
        {
            FirstName = dto.FirstName,
            LastName = dto.LastName,
            DocumentType = dto.DocumentType,
            DocumentNumber = dto.DocumentNumber,
            Email = dto.Email,
            Phone = dto.Phone,
            Nationality = dto.Nationality,
            BirthDate = dto.BirthDate,
            CreatedAt = DateTimeOffset.UtcNow
        };

        await _repository.AddAsync(guest);
        return CreatedAtAction(nameof(GetById), new { id = guest.Id }, new GuestDto 
        { 
            Id = guest.Id, 
            FullName = guest.FullName,
            DocumentNumber = guest.DocumentNumber 
        });
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateCheckInGuestDto dto)
    {
        Console.WriteLine($"[DEBUG] Recibida petición PUT para Guest ID: {id}");
        
        var guest = await _repository.GetByIdAsync(id);
        if (guest == null) 
        {
            Console.WriteLine("[DEBUG] Huésped no encontrado en BD.");
            return NotFound(new { message = "Huésped no encontrado" });
        }

        Console.WriteLine($"[DEBUG] Huésped encontrado: {guest.FullName}. Actualizando datos...");

        // 1. Mapeo Manual
        guest.FirstName = $"{dto.PrimerNombre} {dto.SegundoNombre}".Trim();
        guest.LastName = $"{dto.PrimerApellido} {dto.SegundoApellido}".Trim();
        guest.DocumentNumber = dto.NumeroId;
        guest.Nationality = dto.Nacionalidad;
        
        if (!string.IsNullOrEmpty(dto.Correo)) guest.Email = dto.Correo;
        if (!string.IsNullOrEmpty(dto.Telefono)) guest.Phone = dto.Telefono;

        if (Enum.TryParse<IdType>(dto.TipoId, out var typeEnum))
        {
            guest.DocumentType = typeEnum;
        }

        if (!string.IsNullOrEmpty(dto.FechaNacimiento) && DateTime.TryParse(dto.FechaNacimiento, out var parsedDate))
        {
            guest.BirthDate = DateOnly.FromDateTime(parsedDate);
        }

        try 
        {
            // Forzar actualización explícita
            await _repository.UpdateAsync(guest);
            Console.WriteLine("[DEBUG] UpdateAsync llamado exitosamente.");
            return Ok(new { message = "Datos del huésped actualizados correctamente" });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Error al guardar: {ex.Message}");
            return StatusCode(500, new { message = "Error interno al actualizar", error = ex.Message });
        }
    }
}