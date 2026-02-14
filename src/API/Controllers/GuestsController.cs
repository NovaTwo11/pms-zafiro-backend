using System.Globalization;
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
                
                // --- CORRECCIÓN 1: FALTABA ESTA LÍNEA ---
                CityOfOrigin = g.CityOfOrigin, 
                // ----------------------------------------

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

        // 1. Manejo de Fecha (Sin zona horaria)
        DateOnly? birthDate = null;
        if (!string.IsNullOrEmpty(dto.BirthDate))
        {
            // Intentar parsear "yyyy-MM-dd"
            if (DateOnly.TryParseExact(dto.BirthDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
            {
                birthDate = d;
            }
        }

        // 2. Enum Parse
        var docType = Enum.TryParse<IdType>(dto.DocumentType, true, out var dt) ? dt : IdType.CC;

        var guest = new Guest
        {
            // Concatenación robusta: "Juan" + " " + "David" -> "Juan David"
            FirstName = $"{dto.FirstName} {dto.SecondName}".Trim(),
            LastName = $"{dto.LastName} {dto.SecondLastName}".Trim(),
            
            DocumentType = docType,
            DocumentNumber = dto.DocumentNumber,
            Email = dto.Email,
            Phone = dto.Phone,
            Nationality = dto.Nationality,
            CityOfOrigin = dto.CityOfOrigin, // ¡AQUÍ SE GUARDA LA CIUDAD!
            BirthDate = birthDate,
            CreatedAt = DateTimeOffset.UtcNow
        };

        await _repository.AddAsync(guest);
        
        // Retorno simple
        return Ok(new { id = guest.Id, message = "Huésped creado correctamente" });
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateCheckInGuestDto dto)
    {
        var guest = await _repository.GetByIdAsync(id);
        if (guest == null) return NotFound(new { message = "Huésped no encontrado" });

        // Actualización
        guest.FirstName = $"{dto.PrimerNombre} {dto.SegundoNombre}".Trim();
        guest.LastName = $"{dto.PrimerApellido} {dto.SegundoApellido}".Trim();
        guest.DocumentNumber = dto.NumeroId;
        guest.Nationality = dto.Nacionalidad;
        guest.CityOfOrigin = dto.CiudadOrigen; // ¡AQUÍ SE GUARDA LA CIUDAD!

        if (!string.IsNullOrEmpty(dto.Correo)) guest.Email = dto.Correo;
        if (!string.IsNullOrEmpty(dto.Telefono)) guest.Phone = dto.Telefono;

        if (Enum.TryParse<IdType>(dto.TipoId, true, out var typeEnum))
        {
            guest.DocumentType = typeEnum;
        }

        // Fecha de nacimiento (Mismo fix de zona horaria)
        if (!string.IsNullOrEmpty(dto.FechaNacimiento))
        {
            // Si viene en formato ISO largo (desde el editor) cortamos la T
            var datePart = dto.FechaNacimiento.Contains("T") 
                ? dto.FechaNacimiento.Split('T')[0] 
                : dto.FechaNacimiento;

            if (DateOnly.TryParseExact(datePart, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
            {
                guest.BirthDate = d;
            }
        }

        await _repository.UpdateAsync(guest);
        return Ok(new { message = "Datos actualizados correctamente" });
    }
}