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
            
            StartDate = DateOnly.FromDateTime(r.CheckIn), 
            EndDate = DateOnly.FromDateTime(r.CheckOut),   
            
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
        var reservation = await _repository.GetByIdAsync(id);
        if (reservation == null) return NotFound("Reserva no encontrada");

        if (reservation.Status != ReservationStatus.Pending && reservation.Status != ReservationStatus.Confirmed)
            return BadRequest("El estado de la reserva no permite Check-in.");

        // 1. Actualizar Reserva
        reservation.Status = ReservationStatus.CheckedIn;
        await _repository.UpdateAsync(reservation);

        // 2. Actualizar Habitación (si existe)
        var room = await _roomRepository.GetByIdAsync(reservation.RoomId);
        if (room != null)
        {
            room.Status = RoomStatus.Occupied;
            await _roomRepository.UpdateAsync(room);
        }

        // 3. Crear Folio si no existe
        var existingFolio = await _folioRepository.GetByReservationIdAsync(id);
        if (existingFolio == null)
        {
            var folio = new GuestFolio
            {
                ReservationId = id,
                Status = FolioStatus.Open,
                CreatedAt = DateTimeOffset.UtcNow
            };
            await _folioRepository.AddAsync(folio);
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

        // --- VALIDACIONES DE INTEGRIDAD ---
        if (folio == null) 
        {
            return BadRequest("Error crítico: No se encontró un folio asociado a esta reserva.");
        }

        // 1. Validar Deuda (Margen de 100 pesos por temas de redondeo si aplica)
        if (folio.Balance > 100) 
        {
            return BadRequest(new { 
                error = "DeudaPendiente", 
                message = $"No se puede realizar Check-out. El huésped debe {folio.Balance:C0}" 
            });
        }

        // 2. Lógica de salida anticipada (Actualizar fecha si sale antes)
        if (reservation.CheckOut.Date > DateTime.UtcNow.Date)
        {
            reservation.CheckOut = DateTime.UtcNow;
        }

        // 3. Cambios de estado en memoria
        reservation.Status = ReservationStatus.CheckedOut; 
        folio.Status = FolioStatus.Closed; 

        if (room != null) 
        {
            room.Status = RoomStatus.Dirty;
        }

        // 4. Persistencia segura
        // Si la habitación no existe o es nula, actualizamos individualmente para no romper la transacción
        if (room == null)
        {
             await _repository.UpdateAsync(reservation);
             await _folioRepository.UpdateAsync(folio); 
        }
        else
        {
            // Transacción atómica si todo está correcto
            await _repository.ProcessCheckOutAsync(reservation, room, folio);
        }
        
        // 5. Notificación
        await _notificationRepository.AddAsync(
            "Salida Confirmada",
            $"Habitación {room?.Number ?? "N/A"} liberada.", 
            NotificationType.Warning, 
            "/habitaciones"
        );

        return Ok(new { message = "Check-out exitoso.", newStatus = "CheckedOut" });
    }
}