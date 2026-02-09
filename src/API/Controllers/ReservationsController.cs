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
            
            // Lógica DateOnly del DTO
            StartDate = DateOnly.FromDateTime(r.CheckIn), 
            EndDate = DateOnly.FromDateTime(r.CheckOut),   
            
            Nights = (r.CheckOut - r.CheckIn).Days == 0 ? 1 : (r.CheckOut - r.CheckIn).Days,
            HasFolio = true // Opcional: podrías verificar si r.Folios.Any() si lo incluyes
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
            
            StartDate = DateOnly.FromDateTime(r.CheckIn),
            EndDate = DateOnly.FromDateTime(r.CheckOut),
            
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
            
            // Conversión DateOnly -> DateTime
            CheckIn = dto.StartDate.ToDateTime(TimeOnly.MinValue), 
            CheckOut = dto.EndDate.ToDateTime(TimeOnly.MinValue),    
            
            Status = ReservationStatus.Pending,
            CreatedAt = DateTimeOffset.UtcNow,
            // Generar código si no viene en el DTO o repositorio
            ConfirmationCode = Guid.NewGuid().ToString().Substring(0, 8).ToUpper() 
        };

        // USAMOS CreateAsync (nombre en IReservationRepository)
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
            // USAMOS AddAsync (Asumiendo que IGuestRepository tiene AddAsync, si falla cámbialo a CreateAsync)
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

        // USAMOS CreateAsync (IReservationRepository)
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
        var reservation = await _repository.GetByIdAsync(id);
        if (reservation == null) return NotFound("Reserva no encontrada");

        if (reservation.Status != ReservationStatus.Pending && reservation.Status != ReservationStatus.Confirmed)
            return BadRequest("El estado de la reserva no permite Check-in.");

        reservation.Status = ReservationStatus.CheckedIn;
        await _repository.UpdateAsync(reservation);

        var room = await _roomRepository.GetByIdAsync(reservation.RoomId);
        if (room != null)
        {
            room.Status = RoomStatus.Occupied;
            await _roomRepository.UpdateAsync(room);
        }

        var existingFolio = await _folioRepository.GetByReservationIdAsync(id);
        if (existingFolio == null)
        {
            var folio = new GuestFolio
            {
                ReservationId = id,
                Status = FolioStatus.Open,
                CreatedAt = DateTimeOffset.UtcNow
            };
            
            // --- CORRECCIÓN CRÍTICA ---
            // Usamos AddAsync porque IFolioRepository lo definimos como AddAsync en el paso anterior.
            await _folioRepository.AddAsync(folio);
            // --------------------------
        }

        await _notificationRepository.AddAsync(
            "Check-in Realizado", 
            $"Huésped {reservation.Guest?.FullName} ingresó a habitación {room?.Number}", 
            NotificationType.Info, 
            $"/folios"
        );

        return Ok(new { message = "Check-in exitoso y Folio creado.", status = "CheckedIn" });
    }

    [HttpPost("{id}/checkout")]
    public async Task<IActionResult> CheckOut(Guid id)
    {
        var reservation = await _repository.GetByIdAsync(id);
        if (reservation == null) return NotFound("Reserva no encontrada");

        if (reservation.Status == ReservationStatus.CheckedOut)
            return BadRequest("La reserva ya hizo Check-out.");

        var folio = await _folioRepository.GetByReservationIdAsync(id);
        var room = await _roomRepository.GetByIdAsync(reservation.RoomId);

        // --- VALIDACIÓN DE INTEGRIDAD DE DATOS ---
        if (folio == null) 
        {
            // Si no hay folio, es un error de datos crítico, pero no debería tumbar el servidor.
            return BadRequest("Error crítico: No se encontró un folio asociado a esta reserva.");
        }

        // 1. Validar Deuda
        if (folio.Balance > 100) 
        {
            return BadRequest(new { 
                error = "DeudaPendiente", 
                message = $"No se puede realizar Check-out. El huésped debe {folio.Balance:C0}" 
            });
        }

        // Lógica de salida anticipada...
        if (reservation.CheckOut.Date > DateTime.UtcNow.Date)
        {
            reservation.CheckOut = DateTime.UtcNow;
        }

        // 2. Cambios de estado
        reservation.Status = ReservationStatus.CheckedOut; 
        folio.Status = FolioStatus.Closed; // Ya validamos que folio no es null

        if (room != null) 
        {
            room.Status = RoomStatus.Dirty;
        }

        // 3. Guardar cambios (Pasamos room y folio ya validados)
        // Nota: Si room es null, necesitamos manejarlo en el repositorio o asegurarnos de no enviarlo si es null.
        // Para simplificar y evitar cambiar la firma del repositorio, validemos room antes:
        if (room == null)
        {
             // Si la habitación no existe, solo actualizamos reserva y folio
             // Esto requiere que actualices el repositorio o hagas updates individuales aquí.
             // Opción rápida segura:
             await _repository.UpdateAsync(reservation);
             await _folioRepository.UpdateAsync(folio); // Asumiendo que existe UpdateAsync en FolioRepo
        }
        else
        {
            await _repository.ProcessCheckOutAsync(reservation, room, folio);
        }
        
        // ... Notificaciones y retorno OK ...
        await _notificationRepository.AddAsync(
            "Salida Confirmada",
            $"Habitación {room?.Number ?? "N/A"} liberada.", 
            NotificationType.Warning, 
            "/habitaciones"
        );

        return Ok(new { message = "Check-out exitoso.", newStatus = "CheckedOut" });
    }
}