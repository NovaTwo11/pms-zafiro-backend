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
    
    // ==========================================
    // 1. ENDPOINT: Garantizar Folio (Pagos Pre-Checkin)
    // ==========================================
    [HttpPost("{id}/ensure-folio")]
    public async Task<IActionResult> EnsureFolio(Guid id)
    {
        var reservation = await _repository.GetByIdAsync(id);
        if (reservation == null) return NotFound("Reserva no encontrada");

        // Verificar si ya existe folio
        var existingFolio = await _folioRepository.GetByReservationIdAsync(id);
        if (existingFolio != null) 
            return Ok(new { folioId = existingFolio.Id, message = "Folio ya existente." });

        // Crear Folio "Pre-Stay"
        var folio = new GuestFolio
        {
            ReservationId = id,
            Status = FolioStatus.Open,
            CreatedAt = DateTimeOffset.UtcNow
        };

        // Guardar usando el contexto directamente
        _context.Folios.Add(folio);
        await _context.SaveChangesAsync();

        return Ok(new { folioId = folio.Id, message = "Folio anticipado creado." });
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<ReservationDto>>> GetAll()
    {
        var reservations = await _repository.GetReservationsWithLiveBalanceAsync();
        return Ok(reservations);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ReservationDto>> GetById(Guid id)
    {
        var r = await _context.Reservations
            .Include(x => x.Guest)
            .Include(x => x.ReservationGuests).ThenInclude(rg => rg.Guest)
            .Include(x => x.Segments).ThenInclude(s => s.Room)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (r == null) return NotFound();

        var folio = await _context.Folios.OfType<GuestFolio>()
            .Include(f => f.Transactions)
            .FirstOrDefaultAsync(f => f.ReservationId == id);

        // 2. SOLUCIÓN BALANCE: Calcular sumas absolutas
        var paidAmount = folio?.Transactions
            .Where(t => t.Type == TransactionType.Payment || t.Type == TransactionType.Income)
            .Sum(t => Math.Abs(t.Amount)) ?? 0;

        var charges = folio?.Transactions
            .Where(t => t.Type == TransactionType.Charge || t.Type == TransactionType.Expense)
            .Sum(t => t.Amount) ?? 0;
            
        var balance = charges - paidAmount;

        var mainSegment = r.Segments.OrderBy(s => s.CheckIn).FirstOrDefault();
        var guestsList = new List<GuestDetailDto>();

        // Agregar titular
        if (r.Guest != null)
        {
            // Determinar si está firmado (si hay firma en notas o campo específico)
            // Asumimos que la firma se marca con "[FIRMADO]" en notas por ahora si no hay campo específico
            bool isSigned = r.Status >= ReservationStatus.CheckedIn || (r.Notes != null && r.Notes.Contains("[FIRMADO]"));

            guestsList.Add(new GuestDetailDto
            {
                Id = r.Guest.Id.ToString(), 
                PrimerNombre = r.Guest.FirstName,
                PrimerApellido = r.Guest.LastName,
                Correo = r.Guest.Email,
                Telefono = r.Guest.Phone,
                TipoId = r.Guest.DocumentType.ToString(),
                NumeroId = r.Guest.DocumentNumber,
                Nacionalidad = r.Guest.Nationality,
                EsTitular = true,
                IsSigned = isSigned 
            });
        }

        // Agregar acompañantes
        if (r.ReservationGuests != null)
        {
            foreach (var rg in r.ReservationGuests)
            {
                if (rg.Guest == null) continue;
                guestsList.Add(new GuestDetailDto
                {
                    Id = rg.Guest.Id.ToString(),
                    PrimerNombre = rg.Guest.FirstName,
                    PrimerApellido = rg.Guest.LastName,
                    NumeroId = rg.Guest.DocumentNumber,
                    Nacionalidad = rg.Guest.Nationality,
                    EsTitular = false,
                    IsSigned = true // Acompañantes asumen firma del titular o proceso simple
                });
            }
        } 

        // 3. SOLUCIÓN STEPPER
        int statusStep = 1;
        if (r.Status == ReservationStatus.Confirmed) statusStep = 2;
        else if (r.Status == ReservationStatus.CheckedIn) statusStep = 3;
        else if (r.Status == ReservationStatus.CheckedOut) statusStep = 4;
        else if (r.Status == ReservationStatus.Cancelled || r.Status == ReservationStatus.NoShow) statusStep = 0;

        var dto = new ReservationDto
        {
            Id = r.Id,
            Code = r.ConfirmationCode,
            Status = r.Status.ToString(),
            StatusStep = statusStep,
            
            RoomId = mainSegment?.Room?.Number ?? "?", 
            RoomName = mainSegment?.Room.Category ?? "Habitación",
            
            MainGuestId = r.GuestId,
            MainGuestName = r.Guest?.FullName ?? "Desconocido",
            CheckIn = r.CheckIn,
            CheckOut = r.CheckOut,
            Nights = (r.CheckOut.Date - r.CheckIn.Date).Days == 0 ? 1 : (r.CheckOut.Date - r.CheckIn.Date).Days,
            Adults = r.Adults,
            Children = r.Children,
            CreatedDate = r.CreatedAt.DateTime,
            Notes = r.Notes ?? "",
            TotalAmount = r.TotalAmount,
            PaidAmount = paidAmount,
            Balance = balance,
            FolioId = folio?.Id, 
            
            Segments = r.Segments.Select(s => new ReservationSegmentDto
            {
                RoomId = s.RoomId,
                RoomNumber = s.Room?.Number ?? "?",
                Start = s.CheckIn,
                End = s.CheckOut
            }).ToList(),
            
            Guests = guestsList,
            
            // 4. SOLUCIÓN FINANZAS
            FolioItems = folio?.Transactions.OrderByDescending(t => t.CreatedAt).Select(t => new FolioItemDto
            {
                Id = t.Id,
                Date = t.CreatedAt.ToString("yyyy-MM-dd HH:mm"),
                Concept = t.Description ?? "Movimiento",
                Qty = 1,
                Price = t.Amount, 
                Total = t.Amount
            }).ToList() ?? new List<FolioItemDto>()
        };

        return Ok(dto);
    }

    // ==========================================
    // CREATE: RESERVA MANUAL
    // ==========================================
    [HttpPost]
    public async Task<ActionResult<ReservationDto>> Create(CreateReservationDto dto)
    {
        var room = await _context.Rooms
            .Include(r => r.PriceOverrides)
            .FirstOrDefaultAsync(r => r.Id == dto.RoomId);

        if (room == null) return BadRequest("La habitación no existe.");

        decimal calculatedTotal = 0;
        var checkInDate = dto.CheckIn.Date;
        var checkOutDate = dto.CheckOut.Date;

        if (checkInDate >= checkOutDate)
        {
            var overridePrice = room.PriceOverrides.FirstOrDefault(p => p.Date == DateOnly.FromDateTime(checkInDate));
            calculatedTotal = overridePrice?.Price ?? room.BasePrice;
        }
        else
        {
            for (var date = checkInDate; date < checkOutDate; date = date.AddDays(1))
            {
                var dateOnly = DateOnly.FromDateTime(date);
                var overridePrice = room.PriceOverrides.FirstOrDefault(p => p.Date == dateOnly);
                calculatedTotal += overridePrice?.Price ?? room.BasePrice;
            }
        }

        Guest? guest = null;
        if (!string.IsNullOrEmpty(dto.MainGuestId) && Guid.TryParse(dto.MainGuestId, out Guid guestId))
        {
            guest = await _guestRepository.GetByIdAsync(guestId);
        }
        
        if (guest == null)
        {
            guest = new Guest
            {
                FirstName = dto.MainGuestName ?? "Huésped General",
                LastName = "",
                DocumentType = IdType.CC,
                DocumentNumber = "PENDING-" + Guid.NewGuid().ToString().Substring(0, 4),
                CreatedAt = DateTimeOffset.UtcNow
            };
            await _guestRepository.AddAsync(guest);
        }

        var reservation = new Reservation
        {
            GuestId = guest.Id,
            CheckIn = dto.CheckIn,
            CheckOut = dto.CheckOut,
            Adults = dto.Adults,
            Children = dto.Children,
            Notes = dto.SpecialRequests,
            TotalAmount = calculatedTotal, 
            Status = Enum.TryParse<ReservationStatus>(dto.Status, out var statusParsed) ? statusParsed : ReservationStatus.Pending,
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

        if (reservation.Status != ReservationStatus.Blocked)
        {
            await _notificationRepository.AddAsync(
                "Nueva Reserva Manual",
                $"Reserva {reservation.ConfirmationCode} creada para {guest.FullName} por {calculatedTotal:C}",
                NotificationType.Success,
                $"/reservas/{reservation.Id}"
            );
        }

        return CreatedAtAction(nameof(GetById), new { id = reservation.Id }, new { id = reservation.Id, code = reservation.ConfirmationCode });
    }

    // ==========================================
    // CREATE BOOKING: RESERVA WEB
    // ==========================================
    [HttpPost("booking")]
    public async Task<ActionResult<ReservationDto>> CreateBooking(CreateBookingRequestDto dto)
    {
        var room = await _context.Rooms
            .Include(r => r.PriceOverrides)
            .FirstOrDefaultAsync(r => r.Id == dto.RoomId);

        if (room == null) return BadRequest("La habitación no existe.");

        decimal calculatedTotal = 0;
        var checkInDate = dto.CheckIn.ToDateTime(TimeOnly.MinValue).Date;
        var checkOutDate = dto.CheckOut.ToDateTime(TimeOnly.MinValue).Date;

        if (checkInDate >= checkOutDate)
        {
            var overridePrice = room.PriceOverrides.FirstOrDefault(p => p.Date == DateOnly.FromDateTime(checkInDate));
            calculatedTotal = overridePrice?.Price ?? room.BasePrice;
        }
        else
        {
            for (var date = checkInDate; date < checkOutDate; date = date.AddDays(1))
            {
                var dateOnly = DateOnly.FromDateTime(date);
                var overridePrice = room.PriceOverrides.FirstOrDefault(p => p.Date == dateOnly);
                calculatedTotal += overridePrice?.Price ?? room.BasePrice;
            }
        }

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
            CheckIn = checkInDate,
            CheckOut = checkOutDate,
            TotalAmount = calculatedTotal, 
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
            $"Reserva {reservation.ConfirmationCode} creada para {guest.FullName} por {calculatedTotal:C}",
            NotificationType.Success,
            $"/reservas/{reservation.Id}"
        );

        return CreatedAtAction(nameof(GetById), new { id = reservation.Id }, new { id = reservation.Id, code = reservation.ConfirmationCode });
    }

    [HttpPost("{id}/checkin")]
    public async Task<IActionResult> CheckIn(Guid id)
    {
        // 1. Cargar datos necesarios
        var reservation = await _context.Reservations
            .Include(r => r.Guest)
            .Include(r => r.Segments)
            .FirstOrDefaultAsync(r => r.Id == id);
            
        if (reservation == null) return NotFound("Reserva no encontrada");

        // 2. Validaciones de negocio
        if (reservation.Status != ReservationStatus.Pending && reservation.Status != ReservationStatus.Confirmed)
            return BadRequest($"La reserva está en estado {reservation.Status}, no se puede hacer Check-in.");

        var firstSegment = reservation.Segments.OrderBy(s => s.CheckIn).FirstOrDefault();
        if (firstSegment == null) return BadRequest("Sin habitación asignada.");

        var room = await _roomRepository.GetByIdAsync(firstSegment.RoomId);
        if (room == null) return BadRequest("Habitación no encontrada.");
        
        if (room.Status == RoomStatus.Maintenance || room.Status == RoomStatus.Blocked)
            return BadRequest($"Habitación {room.Number} no disponible ({room.Status}).");

        // 3. Actualizar Estados
        reservation.Status = ReservationStatus.CheckedIn;
        reservation.CheckIn = DateTime.UtcNow; 
        room.Status = RoomStatus.Occupied;

        // 4. Gestión del Folio
        // Usamos .OfType<GuestFolio>() para evitar el error de compilación y acceder a ReservationId
        var folio = await _context.Folios
                                  .OfType<GuestFolio>()
                                  .Include(f => f.Transactions)
                                  .FirstOrDefaultAsync(f => f.ReservationId == id);
        
        // Si no existe, lo creamos
        if (folio == null)
        {
             folio = new GuestFolio
             {
                 Id = Guid.NewGuid(),
                 ReservationId = id,
                 Status = FolioStatus.Open,
                 CreatedAt = DateTimeOffset.UtcNow
             };
             // Importante: Agregamos el folio al contexto inmediatamente
             _context.Folios.Add(folio);
        }

        // 5. GENERAR CARGO AUTOMÁTICO DE ALOJAMIENTO (Solución Directa)
        
        // Verificamos si ya existe el cargo para no duplicarlo
        // Nota: Verificamos en BD o en la lista local si acabamos de cargarlo
        bool chargeExists = folio.Transactions != null && 
                            folio.Transactions.Any(t => t.Type == TransactionType.Charge && t.Description.Contains("Alojamiento"));
        
        if (!chargeExists)
        {
            // Creamos la transacción explícita
            var roomCharge = new FolioTransaction
            {
                Id = Guid.NewGuid(),
                FolioId = folio.Id, // Vinculación directa por ID (Más segura que por navegación)
                Amount = reservation.TotalAmount, 
                Description = $"Cargo por Alojamiento (Total Estadía) - Hab {room.Number}",
                Type = TransactionType.Charge, // Asegúrate que tu Enum tenga Charge=0
                Quantity = 1,
                UnitPrice = reservation.TotalAmount,
                PaymentMethod = PaymentMethod.None,
                CreatedAt = DateTimeOffset.UtcNow,
                CreatedByUserId = "SYSTEM"
            };
            
            // AGREGAR DIRECTAMENTE AL DBSET DE TRANSACCIONES
            // Esto fuerza a EF a insertar el registro sin depender del estado del objeto Folio
            _context.Set<FolioTransaction>().Add(roomCharge);
        }

        try 
        {
            // 6. Guardar todo
            await _context.SaveChangesAsync();
            
            // Notificación
            await _notificationRepository.AddAsync(
                "Check-in Exitoso",
                $"Huésped {reservation.Guest?.FullName} en habitación {room.Number}",
                NotificationType.Success,
                $"/folios?id={folio.Id}"
            );

            return Ok(new { 
                message = "Check-in realizado correctamente", 
                folioId = folio.Id,
                status = "CheckedIn" 
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, "Error interno al procesar Check-in: " + ex.Message);
        }
    }
    
    // ==========================================
    // 2. ACTUALIZADO: Manejo de Firma y Acompañantes
    // ==========================================
    [HttpPut("{id}/guests")]
    public async Task<IActionResult> UpdateGuestInfo(Guid id, [FromBody] UpdateCheckInGuestDto dto)
    {
        // Usamos _context directamente para incluir las relaciones necesarias
        var reservation = await _context.Reservations
            .Include(r => r.Guest)
            .Include(r => r.ReservationGuests) 
            .FirstOrDefaultAsync(r => r.Id == id);

        if (reservation == null) return NotFound(new { message = "Reserva no encontrada" });
        if (reservation.Guest == null) return NotFound(new { message = "El huésped titular no existe en la reserva." });

        // Actualizar Titular
        var mainGuest = reservation.Guest;
        mainGuest.FirstName = dto.PrimerNombre;
        mainGuest.LastName = $"{dto.PrimerApellido} {dto.SegundoApellido}".Trim();
        mainGuest.DocumentNumber = dto.NumeroId;
        mainGuest.Nationality = dto.Nacionalidad;
        mainGuest.Phone = dto.Telefono ?? mainGuest.Phone;
        mainGuest.Email = dto.Correo ?? mainGuest.Email;
        
        if (Enum.TryParse<IdType>(dto.TipoId, out var typeEnum))
        {
            mainGuest.DocumentType = typeEnum;
        }

        // Procesar Firma Digital (Guardado simple en Notas por ahora)
        if (!string.IsNullOrEmpty(dto.SignatureBase64))
        {
            if (reservation.Notes == null) reservation.Notes = "";
            if (!reservation.Notes.Contains("[FIRMADO]"))
            {
                reservation.Notes += $" [FIRMADO: {DateTime.UtcNow:dd/MM/yyyy HH:mm}]";
            }
        }

        // Gestión de Acompañantes
        if (dto.Companions != null)
        {
            if (reservation.ReservationGuests.Any())
            {
                _context.ReservationGuests.RemoveRange(reservation.ReservationGuests);
            }

            foreach (var compDto in dto.Companions)
            {
                if (string.IsNullOrWhiteSpace(compDto.NumeroId)) continue;

                var existingGuest = await _context.Guests
                    .FirstOrDefaultAsync(g => g.DocumentNumber == compDto.NumeroId);

                if (existingGuest == null)
                {
                    existingGuest = new Guest
                    {
                        FirstName = compDto.PrimerNombre,
                        LastName = $"{compDto.PrimerApellido} {compDto.SegundoApellido}".Trim(),
                        DocumentNumber = compDto.NumeroId,
                        Nationality = compDto.Nacionalidad,
                        DocumentType = Enum.TryParse<IdType>(compDto.TipoId, out var t) ? t : IdType.CC,
                        CreatedAt = DateTimeOffset.UtcNow
                    };
                    _context.Guests.Add(existingGuest);
                }
                else
                {
                    existingGuest.FirstName = compDto.PrimerNombre;
                    existingGuest.LastName = $"{compDto.PrimerApellido} {compDto.SegundoApellido}".Trim();
                }

                reservation.ReservationGuests.Add(new ReservationGuest
                {
                    ReservationId = reservation.Id,
                    Guest = existingGuest,
                    IsPrincipal = false
                });
            }
        }

        await _context.SaveChangesAsync();
        
        return Ok(new { message = "Información actualizada correctamente." });
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
    
    [HttpPost("{id}/cancel")]
    public async Task<IActionResult> Cancel(Guid id)
    {
        var reservation = await _context.Reservations.Include(r => r.Segments).FirstOrDefaultAsync(r => r.Id == id);
        if (reservation == null) return NotFound(new { message = "Reserva no encontrada" });

        if (reservation.Status == ReservationStatus.CheckedIn || reservation.Status == ReservationStatus.CheckedOut)
            return BadRequest(new { message = "No se puede cancelar una reserva que ya ingresó o finalizó." });

        reservation.Status = ReservationStatus.Cancelled;
        
        _context.Reservations.Update(reservation);
        await _context.SaveChangesAsync();
        
        return Ok(new { message = "Reserva cancelada correctamente." });
    }

    [HttpPost("{id}/segments/split")]
    public async Task<IActionResult> SplitSegment(Guid id, [FromBody] SplitSegmentDto dto)
    {
        var reservation = await _context.Reservations.Include(r => r.Segments).FirstOrDefaultAsync(r => r.Id == id);
        if (reservation == null) return NotFound(new { message = "Reserva no encontrada" });

        var segments = reservation.Segments.OrderBy(s => s.CheckIn).ToList();
        if (dto.SegmentIndex < 0 || dto.SegmentIndex >= segments.Count) 
            return BadRequest(new { message = "Índice de segmento inválido." });

        var targetSegment = segments[dto.SegmentIndex];

        if (dto.SplitDate <= targetSegment.CheckIn || dto.SplitDate >= targetSegment.CheckOut)
            return BadRequest(new { message = "La fecha de división debe estar estrictamente dentro del rango del segmento actual." });

        var newSegment = new ReservationSegment
        {
            ReservationId = reservation.Id,
            RoomId = dto.NewRoomId ?? targetSegment.RoomId,
            CheckIn = dto.SplitDate,
            CheckOut = targetSegment.CheckOut
        };

        targetSegment.CheckOut = dto.SplitDate;

        _context.Set<ReservationSegment>().Add(newSegment);
        await _context.SaveChangesAsync();

        return Ok(new { message = "Reserva fragmentada exitosamente." });
    }

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

        firstSegment.CheckOut = lastSegment.CheckOut;
        _context.Set<ReservationSegment>().RemoveRange(segments.Skip(1));
        await _context.SaveChangesAsync();

        return Ok(new { message = "Segmentos unificados en la habitación principal." });
    }

    [HttpPost("{id}/segments/{segmentIndex}/move")]
    public async Task<IActionResult> MoveSegment(Guid id, int segmentIndex, [FromBody] MoveSegmentDto dto)
    {
        var reservation = await _context.Reservations.Include(r => r.Segments).FirstOrDefaultAsync(r => r.Id == id);
        if (reservation == null) return NotFound(new { message = "Reserva no encontrada" });

        var segments = reservation.Segments.OrderBy(s => s.CheckIn).ToList();
        if (segmentIndex < 0 || segmentIndex >= segments.Count) 
            return BadRequest(new { message = "Segmento inválido." });

        var targetSegment = segments[segmentIndex];

        bool isOccupied = await _context.Set<ReservationSegment>()
            .AnyAsync(s => s.RoomId == dto.NewRoomId
                        && s.ReservationId != id
                        && s.CheckIn < targetSegment.CheckOut 
                        && s.CheckOut > targetSegment.CheckIn
                        && s.Reservation.Status != ReservationStatus.Cancelled);

        if (isOccupied) 
            return BadRequest(new { message = "La habitación destino está ocupada en esas fechas." });

        targetSegment.RoomId = dto.NewRoomId;
        await _context.SaveChangesAsync();

        return Ok(new { message = "Reserva movida exitosamente." });
    }
}