using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PmsZafiro.Application.DTOs.Reservations;
using PmsZafiro.Application.DTOs.Guests;
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
    private readonly PmsDbContext _context; 

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

    // --- GET ACTUALIZADO: Usa la lógica financiera real ---
    [HttpGet]
    public async Task<ActionResult<IEnumerable<ReservationDto>>> GetAll()
    {
        // Usamos el nuevo método del repositorio que calcula Balance y Pagado
        var reservations = await _repository.GetReservationsWithLiveBalanceAsync();
        return Ok(reservations);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ReservationDto>> GetById(Guid id)
    {
        var r = await _context.Reservations
            .Include(x => x.Guest)
            .Include(x => x.Segments).ThenInclude(s => s.Room)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (r == null) return NotFound();

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
        var reservation = new Reservation
        {
            GuestId = dto.MainGuestId,
            CheckIn = dto.StartDate.ToDateTime(TimeOnly.MinValue),
            CheckOut = dto.EndDate.ToDateTime(TimeOnly.MinValue),
            Status = ReservationStatus.Pending,
            CreatedAt = DateTimeOffset.UtcNow,
            ConfirmationCode = Guid.NewGuid().ToString().Substring(0, 8).ToUpper()
        };

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
        var reservation = await _context.Reservations
            .Include(r => r.Guest)
            .Include(r => r.Segments)
            .FirstOrDefaultAsync(r => r.Id == id);
            
        if (reservation == null) return NotFound("Reserva no encontrada");

        if (reservation.Status != ReservationStatus.Pending && reservation.Status != ReservationStatus.Confirmed)
            return BadRequest($"La reserva está en estado {reservation.Status}, no se puede hacer Check-in.");

        var firstSegment = reservation.Segments.OrderBy(s => s.CheckIn).FirstOrDefault();
        if (firstSegment == null) return BadRequest("La reserva no tiene habitación asignada (segmentos).");

        var room = await _roomRepository.GetByIdAsync(firstSegment.RoomId);
        if (room == null) return BadRequest("La habitación asignada no existe.");
        
        if (room.Status == RoomStatus.Maintenance || room.Status == RoomStatus.Blocked)
            return BadRequest($"La habitación {room.Number} está en {room.Status} y no puede ocuparse.");

        reservation.Status = ReservationStatus.CheckedIn;
        reservation.CheckIn = DateTime.UtcNow; 
        
        room.Status = RoomStatus.Occupied;

        var existingFolio = await _folioRepository.GetByReservationIdAsync(id);
        if (existingFolio != null) 
            return BadRequest("Ya existe un folio para esta reserva.");

        var folio = new GuestFolio
        {
            ReservationId = id,
            Status = FolioStatus.Open,
            CreatedAt = DateTimeOffset.UtcNow
        };

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
        var reservation = await _context.Reservations
            .Include(r => r.Segments)
            .FirstOrDefaultAsync(r => r.Id == id);
            
        if (reservation == null) 
            return NotFound(new { message = "Reserva no encontrada" });

        if (reservation.Status == ReservationStatus.CheckedOut)
            return BadRequest(new { message = "La reserva ya se encuentra en estado Checked-out." });

        var folioInfo = await _context.Folios
            .OfType<GuestFolio>()
            .AsNoTracking()
            .Where(f => f.ReservationId == id)
            .Select(f => new { f.Id })
            .FirstOrDefaultAsync();
        
        if (folioInfo == null) 
            return BadRequest(new { message = "No se encontró el folio asociado a la reserva." });

        // Validación de Deuda contra la Base de Datos (Source of Truth)
        var currentBalance = await _folioRepository.GetFolioBalanceAsync(folioInfo.Id);
        
        decimal toleranceMargin = 100m; 

        if (currentBalance > toleranceMargin)
        {
            return StatusCode(409, new 
            {
                error = "OUTSTANDING_DEBT",
                message = $"El huésped tiene un saldo pendiente de {currentBalance:C0}. Debe saldar la cuenta antes de la salida.",
                currentBalance = currentBalance,
                folioId = folioInfo.Id
            });
        }

        using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            _context.Reservations.Attach(reservation);

            var today = DateTime.UtcNow.Date;
            if (reservation.CheckOut.Date > today)
            {
                reservation.CheckOut = DateTime.UtcNow;

                var futureSegments = reservation.Segments
                    .Where(s => s.CheckIn.Date > today)
                    .ToList();

                var currentSegment = reservation.Segments
                    .FirstOrDefault(s => s.CheckIn.Date <= today && s.CheckOut.Date >= today);

                if (futureSegments.Any())
                {
                    _context.Set<ReservationSegment>().RemoveRange(futureSegments);
                }

                if (currentSegment != null)
                {
                    currentSegment.CheckOut = DateTime.UtcNow;
                    _context.Entry(currentSegment).State = EntityState.Modified;
                }
            }

            reservation.Status = ReservationStatus.CheckedOut;
            
            var folioStub = new GuestFolio { Id = folioInfo.Id, Status = FolioStatus.Closed };
            _context.Folios.Attach(folioStub);
            _context.Entry(folioStub).Property(f => f.Status).IsModified = true;

            var lastOccupiedSegment = reservation.Segments
                .OrderByDescending(s => s.CheckOut)
                .FirstOrDefault(s => s.CheckIn.Date <= DateTime.UtcNow.Date);

            string? roomNumberReleased = null;

            if (lastOccupiedSegment != null)
            {
                var room = await _roomRepository.GetByIdAsync(lastOccupiedSegment.RoomId);
                if (room != null)
                {
                    room.Status = RoomStatus.Dirty; 
                    await _roomRepository.UpdateAsync(room); 
                    roomNumberReleased = room.Number;
                }
            }

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            await _notificationRepository.AddAsync(
                "Check-out Completado",
                $"Salida registrada para reserva {reservation.ConfirmationCode}. Habitación {(roomNumberReleased ?? "N/A")} marcada como Sucia.",
                NotificationType.Warning,
                "/habitaciones"
            );

            return Ok(new { 
                message = "Check-out exitoso.", 
                newStatus = "CheckedOut",
                roomReleased = roomNumberReleased
            });
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            return StatusCode(500, new { message = "Error interno al procesar el check-out: " + ex.Message });
        }
    }
    
    // ==========================================
    // CANCELAR RESERVA
    // ==========================================
    [HttpPost("{id}/cancel")]
    public async Task<IActionResult> Cancel(Guid id)
    {
        var reservation = await _context.Reservations.Include(r => r.Segments).FirstOrDefaultAsync(r => r.Id == id);
        if (reservation == null) return NotFound(new { message = "Reserva no encontrada" });

        if (reservation.Status == ReservationStatus.CheckedIn || reservation.Status == ReservationStatus.CheckedOut)
            return BadRequest(new { message = "No se puede cancelar una reserva que ya ingresó o finalizó." });

        reservation.Status = ReservationStatus.Cancelled;
        
        // Al marcarla como Cancelada, el Frontend la filtrará automáticamente. 
        _context.Reservations.Update(reservation);
        await _context.SaveChangesAsync();
        
        return Ok(new { message = "Reserva cancelada correctamente." });
    }

    // ==========================================
    // DIVIDIR SEGMENTO (SPLIT)
    // ==========================================
    [HttpPost("{id}/segments/split")]
    public async Task<IActionResult> SplitSegment(Guid id, [FromBody] SplitSegmentDto dto)
    {
        var reservation = await _context.Reservations.Include(r => r.Segments).FirstOrDefaultAsync(r => r.Id == id);
        if (reservation == null) return NotFound(new { message = "Reserva no encontrada" });

        var segments = reservation.Segments.OrderBy(s => s.CheckIn).ToList();
        if (dto.SegmentIndex < 0 || dto.SegmentIndex >= segments.Count) 
            return BadRequest(new { message = "Índice de segmento inválido." });

        var targetSegment = segments[dto.SegmentIndex];

        // Validar fechas
        if (dto.SplitDate <= targetSegment.CheckIn || dto.SplitDate >= targetSegment.CheckOut)
            return BadRequest(new { message = "La fecha de división debe estar estrictamente dentro del rango del segmento actual." });

        // Crear el nuevo fragmento
        var newSegment = new ReservationSegment
        {
            ReservationId = reservation.Id,
            RoomId = dto.NewRoomId ?? targetSegment.RoomId,
            CheckIn = dto.SplitDate,
            CheckOut = targetSegment.CheckOut
        };

        // Acortar el fragmento original
        targetSegment.CheckOut = dto.SplitDate;

        _context.Set<ReservationSegment>().Add(newSegment);
        await _context.SaveChangesAsync();

        return Ok(new { message = "Reserva fragmentada exitosamente." });
    }

    // ==========================================
    // UNIFICAR SEGMENTOS (MERGE)
    // ==========================================
    [HttpPost("{id}/segments/merge")]
    public async Task<IActionResult> MergeSegments(Guid id)
    {
        var reservation = await _context.Reservations.Include(r => r.Segments).FirstOrDefaultAsync(r => r.Id == id);
        if (reservation == null) return NotFound(new { message = "Reserva no encontrada" });

        var segments = reservation.Segments.OrderBy(s => s.CheckIn).ToList();
        if (segments.Count <= 1) 
            return BadRequest(new { message = "La reserva no tiene suficientes segmentos para unificar." });

        var firstSegment = segments.First();
        var lastSegment = segments.Last();

        // Extender el primer segmento para cubrir toda la reserva en la habitación inicial
        firstSegment.CheckOut = lastSegment.CheckOut;

        // Eliminar el resto de fragmentos
        _context.Set<ReservationSegment>().RemoveRange(segments.Skip(1));
        await _context.SaveChangesAsync();

        return Ok(new { message = "Segmentos unificados en la habitación principal." });
    }

    // ==========================================
    // MOVER SEGMENTO (DRAG AND DROP)
    // ==========================================
    [HttpPut("{id}/segments/{segmentIndex}/move")]
    public async Task<IActionResult> MoveSegment(Guid id, int segmentIndex, [FromBody] MoveSegmentDto dto)
    {
        var reservation = await _context.Reservations.Include(r => r.Segments).FirstOrDefaultAsync(r => r.Id == id);
        if (reservation == null) return NotFound(new { message = "Reserva no encontrada" });

        var segments = reservation.Segments.OrderBy(s => s.CheckIn).ToList();
        if (segmentIndex < 0 || segmentIndex >= segments.Count) 
            return BadRequest(new { message = "Segmento inválido." });

        var targetSegment = segments[segmentIndex];

        // Validar disponibilidad de la nueva habitación en tiempo real
        bool isOccupied = await _context.Set<ReservationSegment>()
            .AnyAsync(s => s.RoomId == dto.NewRoomId
                        && s.ReservationId != id
                        && s.CheckIn < targetSegment.CheckOut 
                        && s.CheckOut > targetSegment.CheckIn
                        && s.Reservation.Status != ReservationStatus.Cancelled);

        if (isOccupied) 
            return BadRequest(new { message = "La habitación destino está ocupada en esas fechas." });

        // Actualizar habitación
        targetSegment.RoomId = dto.NewRoomId;
        await _context.SaveChangesAsync();

        return Ok(new { message = "Reserva movida exitosamente." });
    }
}