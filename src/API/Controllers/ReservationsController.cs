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
            
            // ✅ FIX: Convertir DateTime (Entidad) -> DateOnly (DTO)
            StartDate = DateOnly.FromDateTime(r.CheckIn), 
            EndDate = DateOnly.FromDateTime(r.CheckOut),   
            
            Nights = (r.CheckOut - r.CheckIn).Days,
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
            
            // ✅ FIX: Convertir DateTime (Entidad) -> DateOnly (DTO)
            StartDate = DateOnly.FromDateTime(r.CheckIn),
            EndDate = DateOnly.FromDateTime(r.CheckOut),
            
            Nights = (r.CheckOut - r.CheckIn).Days,
            HasFolio = true
        };
        return Ok(dto);
    }

    [HttpPost]
    public async Task<ActionResult<ReservationDto>> Create(CreateReservationDto dto)
    {
        // Asumiendo que CreateReservationDto también usa DateOnly para ser consistente con ReservationDto
        var reservation = new Reservation
        {
            GuestId = dto.MainGuestId,
            RoomId = dto.RoomId,
            
            // ✅ FIX: Convertir DateOnly (DTO) -> DateTime (Entidad)
            CheckIn = dto.StartDate.ToDateTime(TimeOnly.MinValue), 
            CheckOut = dto.EndDate.ToDateTime(TimeOnly.MinValue),    
            
            Status = ReservationStatus.Pending,
            CreatedAt = DateTimeOffset.UtcNow
        };

        await _repository.CreateAsync(reservation);
        
        // Retornamos usando el helper para asegurar formato correcto
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
            
            // ✅ FIX: Validamos el tipo de dto.CheckIn. 
            // Si en tu DTO CreateBookingRequestDto usas DateOnly, usa .ToDateTime(TimeOnly.MinValue)
            // Si usas DateTime, déjalo directo. 
            // Aquí asumo DateOnly por consistencia con tu reporte de errores.
            CheckIn = dto.CheckIn.ToDateTime(TimeOnly.MinValue),
            CheckOut = dto.CheckOut.ToDateTime(TimeOnly.MinValue),
            
            Status = ReservationStatus.Pending,
            CreatedAt = DateTimeOffset.UtcNow,
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
                Status = FolioStatus.Open
            };
            await _folioRepository.CreateAsync(folio);
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
        
        if (folio != null && folio.Balance > 0)
        {
            return BadRequest(new { 
                error = "DeudaPendiente", 
                message = $"No se puede realizar Check-out. El huésped debe $ {folio.Balance:N0}" 
            });
        }

        var room = await _roomRepository.GetByIdAsync(reservation.RoomId);
        if (room == null) return BadRequest("Habitación no encontrada");

        await _repository.ProcessCheckOutAsync(reservation, room, folio);
        
        await _notificationRepository.AddAsync(
            "Salida Confirmada" ,
            $"La habitación {room.Number} está libre y requiere limpieza.", 
            NotificationType.Warning, 
            $"/habitaciones"
        );
        
        return Ok(new { message = "Check-out exitoso.", newStatus = "CheckedOut" });
    }
}