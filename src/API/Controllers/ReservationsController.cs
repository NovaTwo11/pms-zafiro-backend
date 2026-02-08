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
    private readonly IFolioRepository _folioRepository; // Inyectamos Folio
    private readonly IRoomRepository _roomRepository;   // Inyectamos Room
    private readonly INotificationRepository _notificationRepository; // Inyectamos Notificaciones

    public ReservationsController(
        IReservationRepository repository,
        IFolioRepository folioRepository,
        IRoomRepository roomRepository,
        INotificationRepository notificationRepository)
    {
        _repository = repository;
        _folioRepository = folioRepository;
        _roomRepository = roomRepository;
        _notificationRepository = notificationRepository;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<ReservationDto>>> GetAll()
    {
        var reservations = await _repository.GetAllAsync();
        var dtos = reservations.Select(r => new ReservationDto
        {
            Id = r.Id,
            Code = r.Code,
            Status = r.Status.ToString(),
            MainGuestId = r.MainGuestId,
            MainGuestName = r.MainGuest != null ? r.MainGuest.FullName : "Sin Nombre",
            RoomId = r.RoomId,
            RoomNumber = r.Room != null ? r.Room.Number : "?",
            StartDate = r.StartDate,
            EndDate = r.EndDate,
            Nights = r.Nights,
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
            Code = r.Code,
            Status = r.Status.ToString(),
            MainGuestId = r.MainGuestId,
            MainGuestName = r.MainGuest.FullName,
            RoomId = r.RoomId,
            RoomNumber = r.Room.Number,
            StartDate = r.StartDate,
            EndDate = r.EndDate,
            Nights = r.Nights,
            HasFolio = true
        };
        return Ok(dto);
    }

    [HttpPost]
    public async Task<ActionResult<ReservationDto>> Create(CreateReservationDto dto)
    {
        var reservation = new Reservation
        {
            MainGuestId = dto.MainGuestId,
            RoomId = dto.RoomId,
            StartDate = dto.StartDate,
            EndDate = dto.EndDate,
            Status = ReservationStatus.Pending
        };

        await _repository.CreateAsync(reservation);
        
        await _notificationRepository.AddAsync(
            "Nueva Reserva", 
            $"Se ha creado la reserva {reservation.Code} para el huésped.", 
            Domain.Enums.NotificationType.Success,
            $"/reservas/{reservation.Id}"
        );
        
        return CreatedAtAction(nameof(GetById), new { id = reservation.Id }, new { id = reservation.Id, code = reservation.Code });
    }

    // --- NUEVO ENDPOINT: CHECK-OUT ---
    [HttpPost("{id}/checkout")]
    public async Task<IActionResult> CheckOut(Guid id)
    {
        // 1. Buscar Reserva
        var reservation = await _repository.GetByIdAsync(id);
        if (reservation == null) return NotFound("Reserva no encontrada");

        if (reservation.Status == ReservationStatus.CheckedOut)
            return BadRequest("La reserva ya hizo Check-out.");

        // 2. Buscar Folio y Validar Deuda
        var folio = await _folioRepository.GetByReservationIdAsync(id);
        if (folio == null) return BadRequest("Error crítico: Reserva sin folio.");
        
        // Calculamos saldo al vuelo
        var balance = folio.Balance; 
        
        if (balance > 0)
        {
            return BadRequest(new { 
                error = "DeudaPendiente", 
                message = $"No se puede realizar Check-out. El huésped debe $ {balance:N0}" 
            });
        }

        // 3. Buscar Habitación
        var room = await _roomRepository.GetByIdAsync(reservation.RoomId);
        if (room == null) return BadRequest("Habitación no encontrada");

        // 4. Ejecutar Proceso
        await _repository.ProcessCheckOutAsync(reservation, room, folio);
        
        await _notificationRepository.AddAsync(
            "Salida Confirmada", 
            $"La habitación {room.Number} está libre y requiere limpieza.", 
            Domain.Enums.NotificationType.Warning, // Warning para llamar la atención
            $"/habitaciones"
        );
        
        return Ok(new { message = "Check-out exitoso. Habitación marcada como Sucia.", newStatus = "CheckedOut" });
    }
}
