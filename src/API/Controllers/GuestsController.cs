using Microsoft.AspNetCore.Mvc;
using PmsZafiro.Application.DTOs.Guests;
using PmsZafiro.Application.Interfaces;
using PmsZafiro.Domain.Entities;

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

    [HttpGet("{id}")]
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

        // Devolvemos el objeto creado (Nota: Idealmente devolveríamos un DTO aquí también)
        return CreatedAtAction(nameof(GetById), new { id = guest.Id }, guest);
    }
}