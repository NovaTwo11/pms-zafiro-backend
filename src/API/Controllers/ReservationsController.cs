using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PmsZafiro.Application.DTOs.Reservations;
using PmsZafiro.Application.DTOs.Guests; // Importante para el UpdateGuestInfo
using PmsZafiro.Application.Interfaces;
using PmsZafiro.Domain.Entities;
using PmsZafiro.Domain.Enums;
using PmsZafiro.Infrastructure.Persistence;

namespace PmsZafiro.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ReservationsController : ControllerBase
{
    private readonly IReservationRepository _repository;
    private readonly IFolioRepository _folioRepository;
    private readonly IRoomRepository _roomRepository;
    private readonly INotificationRepository _notificationRepository;
    private readonly IGuestRepository _guestRepository;
    private readonly PmsDbContext _context; // Necesario para consultas complejas con segmentos

    public ReservationsController(
        IReservationRepository repository,
        IFolioRepository folioRepository,
        IRoomRepository roomRepository,
        INotificationRepository notificationRepository,
        IGuestRepository guestRepository,
        PmsDbContext context)
    {
        _repository = repository;
        _folioRepository = folioRepository;
        _roomRepository = roomRepository;
        _notificationRepository = notificationRepository;
        _guestRepository = guestRepository;
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<ReservationDto>>> GetAll()
    {
        // Incluimos los Segmentos y la Habitación de cada segmento
        var reservations = await _context.Reservations
            .Include(r => r.Guest)
            .Include(r => r.Segments)
            .ThenInclude(s => s.Room)
            .AsNoTracking()
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();

        var dtos = reservations.Select(r => new ReservationDto
        {
            Id = r.Id,
            Code = r.ConfirmationCode,
            Status = r.Status.ToString(),
            MainGuestId = r.GuestId,
            MainGuestName = r.Guest != null ? r.Guest.FullName : "Sin Nombre",
            CheckIn = r.CheckIn,
            CheckOut = r.CheckOut,
            Nights = (r.CheckOut - r.CheckIn).Days == 0 ? 1 : (r.CheckOut - r.CheckIn).Days,
            TotalAmount = r.TotalAmount,
            // Mapeo de segmentos para el frontend
            Segments = r.Segments.Select(s => new ReservationSegmentDto
            {
                RoomId = s.RoomId,
                RoomNumber = s.Room?.Number ?? "?",
                Start = s.CheckIn,
                End = s.CheckOut
            }).ToList()
        });
        
        return Ok(dtos);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ReservationDto>> GetById(Guid id)
    {
        // Usamos el contexto aquí también para asegurar traer los segmentos
        var r = await _context.Reservations
            .Include(x => x.Guest)
            .Include(x => x.Segments).ThenInclude(s => s.Room)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (r == null) return NotFound();

        // Obtenemos la habitación actual o la primera para mostrar en cabecera
        var currentSegment = r.Segments.OrderBy(s => s.CheckIn).FirstOrDefault();

        var dto = new ReservationDto
        {
            Id = r.Id,
            Code = r.ConfirmationCode,
            Status = r.Status.ToString(),
            MainGuestId = r.GuestId,
            MainGuestName = r.Guest?.FullName ?? "Desconocido",
            CheckIn = r.CheckIn,
            CheckOut = r.CheckOut,
            Nights = (r.CheckOut - r.CheckIn).Days == 0 ? 1 : (r.CheckOut - r.CheckIn).Days,
            TotalAmount = r.TotalAmount,
            Segments = r.Segments.Select(s => new ReservationSegmentDto
            {
                RoomId = s.RoomId,
                RoomNumber = s.Room?.Number ?? "?",
                Start = s.CheckIn,
                End = s.CheckOut
            }).ToList()
        };
        return Ok(dto);
    }

    [HttpPost]
    public async Task<ActionResult<ReservationDto>> Create(CreateReservationDto dto)
    {
        // Lógica adaptada para crear el primer segmento automáticamente
        var reservation = new Reservation
        {
            GuestId = dto.MainGuestId,
            CheckIn = dto.StartDate.ToDateTime(TimeOnly.MinValue),
            CheckOut = dto.EndDate.ToDateTime(TimeOnly.MinValue),
            Status = ReservationStatus.Pending,
            CreatedAt = DateTimeOffset.UtcNow,
            ConfirmationCode = Guid.NewGuid().ToString().Substring(0, 8).ToUpper()
        };

        // Crear el segmento inicial
        var segment = new ReservationSegment
        {
            RoomId = dto.RoomId,
            CheckIn = reservation.CheckIn,
            CheckOut = reservation.CheckOut
        };
        reservation.Segments.Add(segment);

        await _repository.CreateAsync(reservation);

        return CreatedAtAction(nameof(GetById), new { id = reservation.Id }, new { id = reservation.Id, code = reservation.ConfirmationCode });
    }

    [HttpPost("booking")]
    public async Task<ActionResult<ReservationDto>> CreateBooking(CreateBookingRequestDto dto)
    {
        Guest? guest = null;

        if (!string.IsNullOrEmpty(dto.DocNumber))
        {
            guest = await _guestRepository.GetByDocumentAsync(dto.DocNumber);
        }

        if (guest == null)
        {
            guest = new Guest
            {
                FirstName = dto.GuestName ?? "Huésped",
                LastName = "",
                Email = dto.GuestEmail ?? "",
                Phone = dto.GuestPhone ?? "",
                DocumentType = Enum.TryParse<IdType>(dto.DocType, out var dt) ? dt : IdType.CC,
                DocumentNumber = dto.DocNumber ?? "SN",
                Nationality = "Colombia",
                CreatedAt = DateTimeOffset.UtcNow
            };
            await _guestRepository.AddAsync(guest);
        }

        var reservation = new Reservation
        {
            GuestId = guest.Id,
            CheckIn = dto.CheckIn.ToDateTime(TimeOnly.MinValue),
            CheckOut = dto.CheckOut.ToDateTime(TimeOnly.MinValue),
            Status = ReservationStatus.Pending,
            CreatedAt = DateTimeOffset.UtcNow,
            ConfirmationCode = Guid.NewGuid().ToString().Substring(0, 8).ToUpper()
        };

        // Segmento inicial
        reservation.Segments.Add(new ReservationSegment
        {
            RoomId = dto.RoomId,
            CheckIn = reservation.CheckIn,
            CheckOut = reservation.CheckOut
        });

        await _repository.CreateAsync(reservation);

        await _notificationRepository.AddAsync(
            "Nueva Reserva Web",
            $"Reserva {reservation.ConfirmationCode} creada para {guest.FullName}",
            NotificationType.Success,
            $"/reservas/{reservation.Id}"
        );

        return CreatedAtAction(nameof(GetById), new { id = reservation.Id }, new { id = reservation.Id, code = reservation.ConfirmationCode });
    }

    [HttpPost("{id}/checkin")]
    public async Task<IActionResult> CheckIn(Guid id)
    {
        // 1. Obtener datos con relaciones (incluyendo segmentos)
        var reservation = await _context.Reservations
            .Include(r => r.Guest)
            .Include(r => r.Segments)
            .FirstOrDefaultAsync(r => r.Id == id);
            
        if (reservation == null) return NotFound("Reserva no encontrada");

        // 2. Validaciones de Estado
        if (reservation.Status != ReservationStatus.Pending && reservation.Status != ReservationStatus.Confirmed)
            return BadRequest($"La reserva está en estado {reservation.Status}, no se puede hacer Check-in.");

        // 3. Determinar qué habitación se ocupa (Primer segmento)
        var firstSegment = reservation.Segments.OrderBy(s => s.CheckIn).FirstOrDefault();
        if (firstSegment == null) return BadRequest("La reserva no tiene habitación asignada (segmentos).");

        var room = await _roomRepository.GetByIdAsync(firstSegment.RoomId);
        if (room == null) return BadRequest("La habitación asignada no existe.");
        
        if (room.Status == RoomStatus.Maintenance || room.Status == RoomStatus.Blocked)
            return BadRequest($"La habitación {room.Number} está en {room.Status} y no puede ocuparse.");

        // 4. Preparar Cambios
        reservation.Status = ReservationStatus.CheckedIn;
        reservation.CheckIn = DateTime.UtcNow; 
        
        room.Status = RoomStatus.Occupied;

        // 5. Preparar Folio
        var existingFolio = await _folioRepository.GetByReservationIdAsync(id);
        if (existingFolio != null) 
            return BadRequest("Ya existe un folio para esta reserva.");

        var folio = new GuestFolio
        {
            ReservationId = id,
            Status = FolioStatus.Open,
            CreatedAt = DateTimeOffset.UtcNow
        };

        // 6. Ejecución
        try 
        {
            await _repository.ProcessCheckInAsync(reservation, room, folio);
            
            await _notificationRepository.AddAsync(
                "Check-in Exitoso",
                $"Huésped {reservation.Guest?.FullName} en habitación {room.Number}",
                NotificationType.Success,
                $"/folios/{folio.Id}"
            );

            return Ok(new { 
                message = "Check-in realizado correctamente", 
                folioId = folio.Id,
                status = "CheckedIn" 
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, "Error interno al procesar el Check-in: " + ex.Message);
        }
    }
    
    [HttpPut("{id}/guests")]
    public async Task<IActionResult> UpdateGuestInfo(Guid id, [FromBody] UpdateCheckInGuestDto dto)
    {
        var reservation = await _repository.GetByIdAsync(id);
        if (reservation == null) return NotFound(new { message = "Reserva no encontrada" });

        var guest = await _guestRepository.GetByIdAsync(reservation.GuestId);
        if (guest == null) return NotFound(new { message = "El huésped titular no existe." });

        guest.FirstName = dto.PrimerNombre;
        guest.LastName = $"{dto.PrimerApellido} {dto.SegundoApellido}".Trim();
        guest.DocumentNumber = dto.NumeroId;
        guest.Nationality = dto.Nacionalidad;
        guest.Phone = dto.Telefono ?? guest.Phone;
        guest.Email = dto.Correo ?? guest.Email;
        
        if (Enum.TryParse<IdType>(dto.TipoId, out var typeEnum))
        {
            guest.DocumentType = typeEnum;
        }

        await _guestRepository.UpdateAsync(guest);
        
        return Ok(new { message = "Información del huésped actualizada correctamente." });
    }

    [HttpPost("{id}/checkout")]
    public async Task<IActionResult> CheckOut(Guid id)
    {
        // 1. Obtener Datos: Incluimos Segmentos y Folio para tomar decisiones
        var reservation = await _context.Reservations
            .Include(r => r.Segments)
            .FirstOrDefaultAsync(r => r.Id == id);
            
        if (reservation == null) return NotFound(new { message = "Reserva no encontrada" });

        if (reservation.Status == ReservationStatus.CheckedOut)
            return BadRequest(new { message = "La reserva ya se encuentra en estado Checked-out." });

        var folio = await _folioRepository.GetByReservationIdAsync(id);
        
        if (folio == null) return BadRequest(new { message = "No se encontró el folio asociado a la reserva." });

        // 2. Validación de Deuda (Regla de Negocio: Margen de $100)
        // Nota: Asumimos que folio.Balance es una propiedad calculada o mantenida por el dominio.
        // Si no, deberíamos sumar folio.Transactions aquí.
        if (folio.Balance > 100)
        {
            return StatusCode(409, new // Usamos 409 Conflict para indicar estado inválido de negocio
            {
                error = "OUTSTANDING_DEBT",
                message = $"El huésped tiene un saldo pendiente de {folio.Balance:C0}. Debe saldar la cuenta antes de la salida.",
                currentBalance = folio.Balance
            });
        }

        // 3. Manejo de Early Departure (Salida Anticipada)
        var today = DateTime.UtcNow.Date;
        if (reservation.CheckOut.Date > today)
        {
            // Ajustar fecha global de la reserva
            reservation.CheckOut = DateTime.UtcNow;

            // Identificar segmentos afectados
            var futureSegments = reservation.Segments
                .Where(s => s.CheckIn.Date > today)
                .ToList();

            var currentSegment = reservation.Segments
                .FirstOrDefault(s => s.CheckIn.Date <= today && s.CheckOut.Date >= today);

            // A. Eliminar segmentos futuros para liberar disponibilidad inmediatamente
            if (futureSegments.Any())
            {
                _context.Set<ReservationSegment>().RemoveRange(futureSegments);
            }

            // B. Recortar el segmento actual para que termine hoy
            if (currentSegment != null)
            {
                currentSegment.CheckOut = DateTime.UtcNow;
                _context.Update(currentSegment); // Marcar como modificado explícitamente
            }
        }

        // 4. Cambios de Estado
        reservation.Status = ReservationStatus.CheckedOut;
        folio.Status = FolioStatus.Closed;

        // 5. Liberación de Habitación (Housekeeping)
        // Buscamos el último segmento válido (el de anoche/hoy) para marcar ESA habitación como sucia.
        var lastOccupiedSegment = reservation.Segments
            .OrderByDescending(s => s.CheckOut)
            .FirstOrDefault(s => s.CheckIn.Date <= DateTime.UtcNow.Date);

        Room? room = null;
        if (lastOccupiedSegment != null)
        {
            room = await _roomRepository.GetByIdAsync(lastOccupiedSegment.RoomId);
            if (room != null)
            {
                room.Status = RoomStatus.Dirty; // Se marca para limpieza
                await _roomRepository.UpdateAsync(room); // Aseguramos actualización
            }
        }

        try
        {
            // Guardamos todos los cambios (Reserva, Segmentos, Folio, Habitación) en una transacción implícita
            // Nota: Si usas el repositorio genérico para guardar, asegúrate de que llame a SaveChangesAsync
            // Aquí usamos el _context directamente para la operación atómica de múltiples entidades modificadas arriba
            await _context.SaveChangesAsync();

            await _notificationRepository.AddAsync(
                "Check-out Completado",
                $"Salida registrada para reserva {reservation.ConfirmationCode}. Habitación {(room?.Number ?? "N/A")} marcada como Sucia.",
                NotificationType.Warning, // Warning para alertar a limpieza
                "/habitaciones"
            );

            return Ok(new { 
                message = "Check-out exitoso.", 
                newStatus = "CheckedOut",
                roomReleased = room?.Number
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Error interno al procesar el check-out: " + ex.Message });
        }
    }
}