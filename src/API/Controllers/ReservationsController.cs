using Microsoft.AspNetCore.Mvc;
using PmsZafiro.Application.DTOs.Reservations;
using PmsZafiro.Application.Interfaces;
using PmsZafiro.Domain.Entities;
using PmsZafiro.Domain.Enums;

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

    public ReservationsController(
        IReservationRepository repository,
        IFolioRepository folioRepository,
        IRoomRepository roomRepository,
        INotificationRepository notificationRepository,
        IGuestRepository guestRepository)
    {
        _repository = repository;
        _folioRepository = folioRepository;
        _roomRepository = roomRepository;
        _notificationRepository = notificationRepository;
        _guestRepository = guestRepository;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<ReservationDto>>> GetAll()
    {
        var reservations = await _repository.GetAllAsync();
        var dtos = reservations.Select(r => new ReservationDto
        {
            Id = r.Id,
            Code = r.ConfirmationCode,
            Status = r.Status.ToString(),
            MainGuestId = r.GuestId,
            MainGuestName = r.Guest != null ? r.Guest.FullName : "Sin Nombre",
            RoomId = r.RoomId,
            RoomNumber = r.Room != null ? r.Room.Number : "?",
            CheckIn = r.CheckIn,
            CheckOut = r.CheckOut,
            Nights = (r.CheckOut - r.CheckIn).Days == 0 ? 1 : (r.CheckOut - r.CheckIn).Days,
            HasFolio = true
        });
        return Ok(dtos);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ReservationDto>> GetById(Guid id)
    {
        var r = await _repository.GetByIdAsync(id);
        if (r == null) return NotFound();

        var dto = new ReservationDto
        {
            Id = r.Id,
            Code = r.ConfirmationCode,
            Status = r.Status.ToString(),
            MainGuestId = r.GuestId,
            MainGuestName = r.Guest?.FullName ?? "Desconocido",
            RoomId = r.RoomId,
            RoomNumber = r.Room?.Number ?? "?",
            CheckIn = r.CheckIn,
            CheckOut = r.CheckOut,
            Nights = (r.CheckOut - r.CheckIn).Days == 0 ? 1 : (r.CheckOut - r.CheckIn).Days,
            HasFolio = true
        };
        return Ok(dto);
    }

    [HttpPost]
    public async Task<ActionResult<ReservationDto>> Create(CreateReservationDto dto)
    {
        var reservation = new Reservation
        {
            GuestId = dto.MainGuestId,
            RoomId = dto.RoomId,
            CheckIn = dto.StartDate.ToDateTime(TimeOnly.MinValue),
            CheckOut = dto.EndDate.ToDateTime(TimeOnly.MinValue),
            Status = ReservationStatus.Pending,
            CreatedAt = DateTimeOffset.UtcNow,
            ConfirmationCode = Guid.NewGuid().ToString().Substring(0, 8).ToUpper()
        };

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
            RoomId = dto.RoomId,
            CheckIn = dto.CheckIn.ToDateTime(TimeOnly.MinValue),
            CheckOut = dto.CheckOut.ToDateTime(TimeOnly.MinValue),
            Status = ReservationStatus.Pending,
            CreatedAt = DateTimeOffset.UtcNow,
            ConfirmationCode = Guid.NewGuid().ToString().Substring(0, 8).ToUpper()
        };

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
        // 1. Obtener datos con relaciones
        var reservation = await _repository.GetByIdAsync(id);
        if (reservation == null) return NotFound("Reserva no encontrada");

        // 2. Validaciones de Estado de Reserva
        if (reservation.Status != ReservationStatus.Pending && reservation.Status != ReservationStatus.Confirmed)
            return BadRequest($"La reserva está en estado {reservation.Status}, no se puede hacer Check-in.");

        // 3. Validaciones de Habitación
        var room = await _roomRepository.GetByIdAsync(reservation.RoomId);
        if (room == null) return BadRequest("La habitación asignada no existe.");
        
        // Regla de Negocio: No permitir Check-in si la habitación está en Mantenimiento o Bloqueada
        if (room.Status == RoomStatus.Maintenance || room.Status == RoomStatus.Blocked)
            return BadRequest($"La habitación {room.Number} está en {room.Status} y no puede ocuparse.");

        // 4. Preparar Cambios (En memoria)
        reservation.Status = ReservationStatus.CheckedIn;
        reservation.CheckIn = DateTime.UtcNow; // Ajustar hora real de entrada
        
        room.Status = RoomStatus.Occupied;

        // 5. Preparar Folio
        var existingFolio = await _folioRepository.GetByReservationIdAsync(id);
        if (existingFolio != null) 
            return BadRequest("Ya existe un folio para esta reserva. Error de consistencia.");

        var folio = new GuestFolio
        {
            ReservationId = id,
            Status = FolioStatus.Open,
            CreatedAt = DateTimeOffset.UtcNow
        };

        // 6. Ejecución Atómica
        try 
        {
            await _repository.ProcessCheckInAsync(reservation, room, folio);
            
            // Notificación (Fuera de la transacción crítica)
            await _notificationRepository.AddAsync(
                "Check-in Exitoso",
                $"Huésped {reservation.Guest?.FullName} en habitación {room.Number}",
                NotificationType.Success,
                $"/folios/{folio.Id}" // Enlace directo al folio
            );

            return Ok(new { 
                message = "Check-in realizado correctamente", 
                folioId = folio.Id,
                status = "CheckedIn" 
            });
        }
        catch (Exception ex)
        {
            // Log error
            return StatusCode(500, "Error interno al procesar el Check-in: " + ex.Message);
        }
    }

    [HttpPost("{id}/checkout")]
    public async Task<IActionResult> CheckOut(Guid id)
    {
        Console.WriteLine($"[CheckOut] Procesando solicitud para ID: {id}");

        // 1. Obtener Datos
        var reservation = await _repository.GetByIdAsync(id);
        if (reservation == null) return NotFound(new { message = "Reserva no encontrada" });

        if (reservation.Status == ReservationStatus.CheckedOut)
            return BadRequest(new { message = "La reserva ya hizo Check-out." });

        var folio = await _folioRepository.GetByReservationIdAsync(id);
        
        // 2. Validación de Integridad
        if (folio == null)
        {
            Console.WriteLine($"[CheckOut] Error Crítico: No se encontró folio para reserva {id}");
            return BadRequest(new { message = "Error crítico: No se encontró un folio asociado a esta reserva." });
        }

        // 3. Validar Deuda (Uso Math.Abs para asegurar saldo cero exacto)
        if (Math.Abs(folio.Balance) > 100)
        {
            return BadRequest(new
            {
                error = "DeudaPendiente",
                message = $"No se puede realizar Check-out. El huésped debe {folio.Balance:C0}"
            });
        }

        // 4. Lógica de Fechas (Salida anticipada)
        if (reservation.CheckOut.Date > DateTime.UtcNow.Date)
        {
            reservation.CheckOut = DateTime.UtcNow;
        }

        // 5. Cambios de Estado
        reservation.Status = ReservationStatus.CheckedOut;
        folio.Status = FolioStatus.Closed;

        var room = await _roomRepository.GetByIdAsync(reservation.RoomId);
        if (room != null)
        {
            room.Status = RoomStatus.Dirty;
        }
        else 
        {
            Console.WriteLine("[CheckOut] Advertencia: La reserva no tiene habitación válida vinculada (null).");
        }

        // 6. Transacción Atómica
        try
        {
            // El repositorio maneja la lógica segura incluso si room es null
            await _repository.ProcessCheckOutAsync(reservation, room, folio);

            await _notificationRepository.AddAsync(
                "Salida Confirmada",
                $"Habitación {room?.Number ?? "N/A"} liberada.",
                NotificationType.Warning,
                "/habitaciones"
            );

            return Ok(new { message = "Check-out exitoso.", newStatus = "CheckedOut" });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[CheckOut] Excepción al guardar: {ex.Message}");
            return StatusCode(500, new { message = "Error interno al procesar el check-out." });
        }
    }
}