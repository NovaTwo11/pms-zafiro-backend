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
    private readonly IEmailService _emailService;
    private readonly IConfiguration _config;

    public ReservationsController(
        IReservationRepository repository,
        IFolioRepository folioRepository,
        IRoomRepository roomRepository,
        INotificationRepository notificationRepository,
        IGuestRepository guestRepository,
        PmsDbContext context,
        IEmailService emailService,
        IConfiguration config)
    {
        _repository = repository;
        _folioRepository = folioRepository;
        _roomRepository = roomRepository;
        _notificationRepository = notificationRepository;
        _guestRepository = guestRepository;
        _context = context;
        _emailService = emailService;
        _config = config;
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

        // --- Lógica Financiera (Folio y Balance) ---
        var folio = await _context.Folios.OfType<GuestFolio>()
            .Include(f => f.Transactions)
            .FirstOrDefaultAsync(f => f.ReservationId == id);

        var paidAmount = folio?.Transactions
            .Where(t => t.Type == TransactionType.Payment || t.Type == TransactionType.Income)
            .Sum(t => Math.Abs(t.Amount)) ?? 0;

        var charges = folio?.Transactions
            .Where(t => t.Type == TransactionType.Charge || t.Type == TransactionType.Expense)
            .Sum(t => t.Amount) ?? 0;
            
        var realTotal = charges > 0 ? charges : r.TotalAmount;
        var balance = realTotal - paidAmount;

        if (r.Status == ReservationStatus.Pending && paidAmount > 0)
        {
            r.Status = ReservationStatus.Confirmed;
            await _context.SaveChangesAsync(); 
        }

        // --- Helper Local para Nombres ---
        (string p, string s) SplitName(string fullName)
        {
            if (string.IsNullOrWhiteSpace(fullName)) return ("", "");
            var parts = fullName.Trim().Split(' ', 2);
            return (parts[0], parts.Length > 1 ? parts[1] : "");
        }

        var guestsList = new List<GuestDetailDto>();

        if (r.Guest != null)
        {
            var (nom1, nom2) = SplitName(r.Guest.FirstName);
            var (ape1, ape2) = SplitName(r.Guest.LastName);
            bool isSigned = r.Status >= ReservationStatus.CheckedIn || (r.Notes != null && r.Notes.Contains("[FIRMADO]"));

            guestsList.Add(new GuestDetailDto
            {
                Id = r.Guest.Id.ToString(),
                PrimerNombre = nom1,
                SegundoNombre = nom2,
                PrimerApellido = ape1,
                SegundoApellido = ape2,
                Correo = r.Guest.Email,
                Telefono = r.Guest.Phone,
                TipoId = r.Guest.DocumentType.ToString(),
                NumeroId = r.Guest.DocumentNumber,
                Nacionalidad = r.Guest.Nationality,
                FechaNacimiento = r.Guest.BirthDate?.ToDateTime(TimeOnly.MinValue),
                EsTitular = true,
                IsSigned = isSigned 
            });
        }

        if (r.ReservationGuests != null)
        {
            foreach (var rg in r.ReservationGuests)
            {
                if (rg.Guest == null) continue;
                
                // CORRECCIÓN: Omitir al titular para que no aparezca como acompañante
                if (rg.Guest.Id == r.GuestId) continue; 

                var (n1, n2) = SplitName(rg.Guest.FirstName);
                var (a1, a2) = SplitName(rg.Guest.LastName);

                guestsList.Add(new GuestDetailDto
                {
                    Id = rg.Guest.Id.ToString(),
                    PrimerNombre = n1,
                    SegundoNombre = n2,
                    PrimerApellido = a1,
                    SegundoApellido = a2,
                    TipoId = rg.Guest.DocumentType.ToString(),
                    NumeroId = rg.Guest.DocumentNumber,
                    Nacionalidad = rg.Guest.Nationality,
                    EsTitular = false,
                    IsSigned = true 
                });
            }
        } 

        var mainSegment = r.Segments.OrderBy(s => s.CheckIn).FirstOrDefault();
        int statusStep = r.Status switch
        {
            ReservationStatus.Confirmed => 2,
            ReservationStatus.CheckedIn => 3,
            ReservationStatus.CheckedOut => 4,
            ReservationStatus.Cancelled or ReservationStatus.NoShow => 0,
            _ => 1 
        };

        return Ok(new ReservationDto
        {
            Id = r.Id,
            Code = r.ConfirmationCode,
            Status = r.Status.ToString(),
            StatusStep = statusStep,
            RoomId = mainSegment?.Room?.Number ?? "?", 
            RoomName = mainSegment?.Room?.Category ?? "Habitación",
            MainGuestId = r.GuestId,
            MainGuestName = r.Guest?.FullName ?? "Desconocido",
            CheckIn = r.CheckIn,
            CheckOut = r.CheckOut,
            Nights = (r.CheckOut.Date - r.CheckIn.Date).Days > 0 ? (r.CheckOut.Date - r.CheckIn.Date).Days : 1,
            Adults = r.Adults,
            Children = r.Children,
            CreatedDate = r.CreatedAt.DateTime,
            Notes = r.Notes ?? "",
            TotalAmount = realTotal, 
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
            FolioItems = folio?.Transactions.OrderByDescending(t => t.CreatedAt).Select(t => new FolioItemDto
            {
                Id = t.Id,
                Date = t.CreatedAt.ToString("yyyy-MM-dd HH:mm"),
                Concept = t.Description ?? "Movimiento",
                Qty = 1,
                Price = t.Amount, 
                Total = t.Amount
            }).ToList() ?? new List<FolioItemDto>()
        });
    }

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

        if (dto.Status == "Blocked" || dto.Status == "Maintenance")
        {
            guest = await _guestRepository.GetByDocumentAsync("SYS-BLOCK");
            if (guest == null)
            {
                guest = new Guest
                {
                    FirstName = "BLOQUEO",
                    LastName = "SISTEMA",
                    DocumentType = IdType.CC,
                    DocumentNumber = "SYS-BLOCK",
                    Nationality = "System",
                    CreatedAt = DateTimeOffset.UtcNow
                };
                await _guestRepository.AddAsync(guest);
            }
        }
        else 
        {
            if (!string.IsNullOrEmpty(dto.MainGuestId) && Guid.TryParse(dto.MainGuestId, out Guid guestId))
                guest = await _guestRepository.GetByIdAsync(guestId);
            
            if (guest == null && !string.IsNullOrEmpty(dto.GuestDocNumber))
                guest = await _guestRepository.GetByDocumentAsync(dto.GuestDocNumber);
            
            if (guest == null)
            {
                DateOnly? birthDateParsed = null;
                if (!string.IsNullOrEmpty(dto.GuestBirthDate))
                {
                    var datePart = dto.GuestBirthDate.Split('T')[0]; 
                    DateOnly.TryParse(datePart, out var bd);
                    birthDateParsed = bd;
                }

                guest = new Guest
                {
                    FirstName = $"{dto.GuestFirstName} {dto.GuestSecondName}".Trim(),
                    LastName = $"{dto.GuestLastName} {dto.GuestSecondLastName}".Trim(),
                    DocumentType = Enum.TryParse<IdType>(dto.GuestDocType, out var dt) ? dt : IdType.CC,
                    DocumentNumber = !string.IsNullOrEmpty(dto.GuestDocNumber) ? dto.GuestDocNumber : "PEND-" + Guid.NewGuid().ToString()[..4],
                    Email = dto.GuestEmail ?? "",
                    Phone = dto.GuestPhone ?? "",
                    Nationality = "Colombia", 
                    BirthDate = birthDateParsed,
                    CityOfOrigin = dto.GuestCityOrigin,
                    CreatedAt = DateTimeOffset.UtcNow
                };
                await _guestRepository.AddAsync(guest);
            }
            else 
            {
                if (!string.IsNullOrEmpty(dto.GuestEmail)) guest.Email = dto.GuestEmail;
                if (!string.IsNullOrEmpty(dto.GuestPhone)) guest.Phone = dto.GuestPhone;
                await _context.SaveChangesAsync();
            }
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
                "Nueva Reserva",
                $"Reserva {reservation.ConfirmationCode} creada para {guest.FullName}",
                NotificationType.Success,
                $"/reservas/{reservation.Id}"
            );
        }

        return CreatedAtAction(nameof(GetById), new { id = reservation.Id }, new { id = reservation.Id, code = reservation.ConfirmationCode });
    }

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
            guest = await _guestRepository.GetByDocumentAsync(dto.DocNumber);

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
        var reservation = await _context.Reservations
            .Include(r => r.Guest)
            .Include(r => r.Segments)
            .FirstOrDefaultAsync(r => r.Id == id);
            
        if (reservation == null) return NotFound("Reserva no encontrada");

        if (reservation.Status != ReservationStatus.Pending && reservation.Status != ReservationStatus.Confirmed)
            return BadRequest($"La reserva está en estado {reservation.Status}, no se puede hacer Check-in.");

        var firstSegment = reservation.Segments.OrderBy(s => s.CheckIn).FirstOrDefault();
        if (firstSegment == null) return BadRequest("Sin habitación asignada.");

        var room = await _roomRepository.GetByIdAsync(firstSegment.RoomId);
        if (room == null) return BadRequest("Habitación no encontrada.");
        
        if (room.Status == RoomStatus.Maintenance || room.Status == RoomStatus.Blocked)
            return BadRequest($"Habitación {room.Number} no disponible ({room.Status}).");

        reservation.Status = ReservationStatus.CheckedIn;
        reservation.CheckIn = DateTime.UtcNow; 
        room.Status = RoomStatus.Occupied;

        var folio = await _context.Folios
                                  .OfType<GuestFolio>()
                                  .Include(f => f.Transactions)
                                  .FirstOrDefaultAsync(f => f.ReservationId == id);
        
        if (folio == null)
        {
             folio = new GuestFolio
             {
                 Id = Guid.NewGuid(),
                 ReservationId = id,
                 Status = FolioStatus.Open,
                 CreatedAt = DateTimeOffset.UtcNow
             };
             _context.Folios.Add(folio);
        }

        bool chargeExists = folio.Transactions != null && 
                            folio.Transactions.Any(t => t.Type == TransactionType.Charge && t.Description.Contains("Alojamiento"));
        
        if (!chargeExists)
        {
            var roomCharge = new FolioTransaction
            {
                Id = Guid.NewGuid(),
                FolioId = folio.Id, 
                Amount = reservation.TotalAmount, 
                Description = $"Cargo por Alojamiento (Total Estadía) - Hab {room.Number}",
                Type = TransactionType.Charge, 
                Quantity = 1,
                UnitPrice = reservation.TotalAmount,
                PaymentMethod = PaymentMethod.None,
                CreatedAt = DateTimeOffset.UtcNow,
                CreatedByUserId = "SYSTEM"
            };
            
            _context.Set<FolioTransaction>().Add(roomCharge);
        }

        try 
        {
            await _context.SaveChangesAsync();
            
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
    
    [HttpPut("{id}/guests")]
    public async Task<IActionResult> UpdateGuestInfo(Guid id, [FromBody] UpdateCheckInGuestDto dto)
    {
        var reservation = await _context.Reservations
            .Include(r => r.Guest)
            .Include(r => r.ReservationGuests).ThenInclude(rg => rg.Guest)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (reservation == null) return NotFound(new { message = "Reserva no encontrada" });
        if (reservation.Guest == null) return NotFound(new { message = "El huésped titular no existe (Error de integridad)." });

        var g = reservation.Guest;
        g.FirstName = $"{dto.PrimerNombre} {dto.SegundoNombre}".Trim(); 
        g.LastName = $"{dto.PrimerApellido} {dto.SegundoApellido}".Trim();
        g.DocumentNumber = dto.NumeroId;
        g.Nationality = dto.Nacionalidad;
        
        if (!string.IsNullOrEmpty(dto.Telefono)) g.Phone = dto.Telefono;
        if (!string.IsNullOrEmpty(dto.Correo)) g.Email = dto.Correo;
        if (!string.IsNullOrEmpty(dto.CiudadOrigen)) g.CityOfOrigin = dto.CiudadOrigen; 
        if (Enum.TryParse<IdType>(dto.TipoId, out var typeEnum)) g.DocumentType = typeEnum;

        if (!string.IsNullOrEmpty(dto.FechaNacimiento))
        {
            var datePart = dto.FechaNacimiento.Split('T')[0]; 
            if (DateOnly.TryParse(datePart, out var bd)) g.BirthDate = bd;
        }

        if (!string.IsNullOrEmpty(dto.SignatureBase64))
        {
            if (reservation.Notes == null) reservation.Notes = "";
            if (!reservation.Notes.Contains("[FIRMADO]"))
                reservation.Notes += $" [FIRMADO: {DateTime.UtcNow:dd/MM/yyyy HH:mm}]";
        }

        if (dto.Companions != null)
        {
            if (reservation.ReservationGuests.Any())
                _context.ReservationGuests.RemoveRange(reservation.ReservationGuests);

            foreach (var compDto in dto.Companions)
            {
                if (string.IsNullOrWhiteSpace(compDto.NumeroId)) continue;

                // CORRECCIÓN: Bloqueo de seguridad. Evita que un error en el frontend 
                // cause que un acompañante sobrescriba los datos del titular.
                if (compDto.NumeroId == dto.NumeroId) continue;

                var existingGuest = await _context.Guests.FirstOrDefaultAsync(g => g.DocumentNumber == compDto.NumeroId);

                DateOnly? compBirthDate = null;
                if (!string.IsNullOrEmpty(compDto.FechaNacimiento))
                {
                    var datePart = compDto.FechaNacimiento.Split('T')[0];
                    if (DateOnly.TryParse(datePart, out var bd)) compBirthDate = bd;
                }

                if (existingGuest == null)
                {
                    existingGuest = new Guest
                    {
                        FirstName = $"{compDto.PrimerNombre} {compDto.SegundoNombre}".Trim(),
                        LastName = $"{compDto.PrimerApellido} {compDto.SegundoApellido}".Trim(),
                        DocumentNumber = compDto.NumeroId,
                        Nationality = compDto.Nacionalidad,
                        DocumentType = Enum.TryParse<IdType>(compDto.TipoId, out var t) ? t : IdType.CC,
                        CityOfOrigin = compDto.CiudadOrigen, 
                        BirthDate = compBirthDate, 
                        CreatedAt = DateTimeOffset.UtcNow
                    };
                    _context.Guests.Add(existingGuest);
                }
                else
                {
                    existingGuest.FirstName = $"{compDto.PrimerNombre} {compDto.SegundoNombre}".Trim();
                    existingGuest.LastName = $"{compDto.PrimerApellido} {compDto.SegundoApellido}".Trim();
                    if (!string.IsNullOrEmpty(compDto.Nacionalidad)) existingGuest.Nationality = compDto.Nacionalidad;
                    if (!string.IsNullOrEmpty(compDto.CiudadOrigen)) existingGuest.CityOfOrigin = compDto.CiudadOrigen; 
                    if (compBirthDate.HasValue) existingGuest.BirthDate = compBirthDate; 
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
        return Ok(new { message = "Información de huéspedes actualizada correctamente." });
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
    
    [HttpPost("{id}/send-summary")]
    public async Task<IActionResult> SendReservationSummary(Guid id)
    {
        var r = await _context.Reservations
            .Include(x => x.Guest)
            .Include(x => x.Segments).ThenInclude(s => s.Room)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (r == null || r.Guest == null || string.IsNullOrEmpty(r.Guest.Email)) 
            return BadRequest("Reserva o correo de huésped no encontrado.");

        var roomInfo = r.Segments.FirstOrDefault()?.Room?.Number ?? "N/A";
        
        // Plantilla HTML profesional
        var body = $@"
        <div style='font-family: ""Segoe UI"", Arial, sans-serif; max-width: 650px; margin: 0 auto; border: 1px solid #e0e0e0; border-radius: 12px; overflow: hidden; box-shadow: 0 4px 15px rgba(0,0,0,0.05);'>
            <div style='background-color: #D4AF37; padding: 25px; text-align: center; color: white;'>
                <h1 style='margin: 0; font-size: 28px; letter-spacing: 2px;'>HOTEL ZAFIRO</h1>
                <p style='margin: 5px 0 0 0; font-size: 16px; opacity: 0.9;'>Confirmación de Reserva</p>
            </div>
            <div style='padding: 30px; background-color: #ffffff; color: #333333;'>
                <p style='font-size: 16px;'>Estimado/a <strong>{r.Guest.FirstName}</strong>,</p>
                <p style='font-size: 16px; color: #555;'>Su reserva ha sido confirmada. Nos emociona recibirle pronto. A continuación encontrará los detalles de su estadía:</p>
                
                <div style='background-color: #f8f9fa; border-left: 4px solid #D4AF37; padding: 20px; border-radius: 4px; margin: 25px 0;'>
                    <p style='margin: 8px 0; font-size: 15px;'><strong>Código de Reserva:</strong> <span style='color: #D4AF37; font-size: 18px; font-weight: bold; margin-left: 5px;'>{r.ConfirmationCode}</span></p>
                    <p style='margin: 8px 0; font-size: 15px;'><strong>Habitación:</strong> {roomInfo}</p>
                    <p style='margin: 8px 0; font-size: 15px;'><strong>Check-in:</strong> {r.CheckIn:dd MMM yyyy} a partir de las <strong>15:00</strong></p>
                    <p style='margin: 8px 0; font-size: 15px;'><strong>Check-out:</strong> {r.CheckOut:dd MMM yyyy} hasta las <strong>12:00</strong></p>
                    <p style='margin: 8px 0; font-size: 15px;'><strong>Total de la Estadía:</strong> <span style='font-size: 16px; font-weight: bold;'>{r.TotalAmount:C}</span></p>
                </div>

                <h3 style='color: #D4AF37; border-bottom: 2px solid #f0f0f0; padding-bottom: 8px; margin-top: 30px;'>Impuestos y cargos adicionales</h3>
                <ul style='font-size: 14px; color: #555; padding-left: 20px;'>
                    <li style='margin-bottom: 6px;'>Los niños menores de 5 años pagan el mismo valor de hospedaje sin importar la edad.</li>
                    <li style='margin-bottom: 6px;'>La tarifa <strong>NO</strong> incluye el Impuesto al Valor Agregado (IVA).</li>
                </ul>

                <h3 style='color: #D4AF37; border-bottom: 2px solid #f0f0f0; padding-bottom: 8px; margin-top: 25px;'>Condiciones del servicio</h3>
                <ul style='font-size: 14px; color: #555; padding-left: 20px;'>
                    <li style='margin-bottom: 6px;'>Nuestro motor de reservas te cobrará únicamente el <strong>50%</strong> del valor total de tu reserva para confirmarla. En caso de agregar servicios extra, estos serán cobrados en su totalidad.</li>
                    <li style='margin-bottom: 6px;'>El valor restante debe cancelarse <strong>en</strong> el hotel al momento de realizar el check-in.</li>
                </ul>

                <h3 style='color: #D4AF37; border-bottom: 2px solid #f0f0f0; padding-bottom: 8px; margin-top: 25px;'>Niños, Niñas y Adolescentes</h3>
                <p style='font-size: 14px; color: #555; margin: 10px 0;'>Todos los menores de edad deben presentar su documento de identificación al momento del Check-in y deberán venir acompañados por unos de sus padres o de lo contrario traer la autorización debidamente firmada por sus padres con una copia de sus documentos de identificación.</p>

                <h3 style='color: #D4AF37; border-bottom: 2px solid #f0f0f0; padding-bottom: 8px; margin-top: 25px;'>Mascotas</h3>
                <p style='font-size: 14px; color: #555; margin: 10px 0;'>No aceptamos mascotas en nuestras instalaciones.</p>

                <h3 style='color: #D4AF37; border-bottom: 2px solid #f0f0f0; padding-bottom: 8px; margin-top: 25px;'>Políticas de Cancelación y Reprogramación</h3>
                <p style='font-size: 14px; color: #555; margin: 10px 0;'>No realizamos devolución de dinero por cancelación de reserva. Usted podrá reprogramar su(s) reserva(s) sin penalidad, si lo hace al menos 3 días antes de la fecha de llegada, en caso de no hacer uso de su reserva y no haberla reprogramado dentro del tiempo establecido, le será cobrada una penalidad por el valor de la primera noche de alojamiento más impuestos y la totalidad de servicios adicionales reservados.</p>
                <p style='font-size: 14px; color: #555; margin: 10px 0;'>Puede reprogramar su reserva contactándonos al teléfono <strong>+57 3202095352</strong> o mediante el correo electrónico <strong>zafirohoteldoradal@gmail.com</strong>.</p>
                <p style='font-size: 14px; color: #555; margin: 10px 0;'>Usted podrá reprogramar su reserva únicamente 1 vez y debe cumplir las siguientes condiciones:</p>
                <ul style='font-size: 14px; color: #555; padding-left: 20px; margin-top: 5px;'>
                    <li style='margin-bottom: 4px;'>Debe conservar el mismo tipo de habitación.</li>
                    <li style='margin-bottom: 4px;'>Debe conservar el mismo número de noches, o elegir una cantidad superior.</li>
                    <li style='margin-bottom: 4px;'>Debe conservar el mismo número de huéspedes.</li>
                    <li style='margin-bottom: 4px;'>Si aceptamos su solicitud de reprogramación, tiene un plazo máximo de 30 días hábiles para hacer efectiva su reserva.</li>
                </ul>
                <p style='font-size: 14px; color: #555; margin: 10px 0;'>Las solicitudes de reprogramación o cesión de reserva a terceros únicamente serán aceptadas si son realizadas por el titular de la reserva.</p>
            </div>
            <div style='background-color: #111111; padding: 20px; text-align: center; color: #aaaaaa; font-size: 13px;'>
                <p style='margin: 0 0 5px 0; color: #ffffff; font-weight: bold; letter-spacing: 1px;'>HOTEL ZAFIRO DORADAL</p>
                <p style='margin: 0;'>+57 3202095352 • zafirohoteldoradal@gmail.com</p>
            </div>
        </div>";

        await _emailService.SendEmailAsync(r.Guest.Email, $"Reserva Confirmada #{r.ConfirmationCode}", body);
        return Ok(new { message = "Resumen enviado exitosamente." });
    }

    [HttpPost("{id}/send-checkin-link")]
    public async Task<IActionResult> SendCheckInLink(Guid id)
    {
        var r = await _context.Reservations.Include(x => x.Guest).FirstOrDefaultAsync(x => x.Id == id);
        if (r == null || r.Guest == null || string.IsNullOrEmpty(r.Guest.Email)) 
            return BadRequest("Reserva o correo no encontrado.");

        var baseUrl = _config["FrontendUrl"] ?? "http://localhost:3000"; 
        var link = $"{baseUrl}/guest/check-in/{r.ConfirmationCode ?? r.Id.ToString()}";

        // Plantilla HTML de Alta Conversión para Check-in
        var body = $@"
        <div style='font-family: ""Segoe UI"", Tahoma, Geneva, Verdana, sans-serif; max-width: 600px; margin: 0 auto; border-radius: 12px; overflow: hidden; box-shadow: 0 4px 20px rgba(0,0,0,0.08); border: 1px solid #eaeaea; background-color: #ffffff;'>
            
            <div style='background-color: #111111; padding: 35px 20px; text-align: center; border-bottom: 4px solid #D4AF37;'>
                <h1 style='color: #D4AF37; margin: 0; font-size: 28px; letter-spacing: 3px; text-transform: uppercase;'>HOTEL ZAFIRO</h1>
                <p style='color: #ffffff; margin: 8px 0 0 0; font-size: 14px; opacity: 0.8; letter-spacing: 1px;'>PRE-REGISTRO DE HUÉSPEDES</p>
            </div>

            <div style='padding: 40px 30px; text-align: center;'>
                <h2 style='color: #333333; margin-top: 0; font-size: 24px; font-weight: 700;'>¡Su estadía está muy cerca!</h2>
                <p style='color: #555555; font-size: 16px; line-height: 1.6; margin-bottom: 25px;'>
                    Estimado/a <strong>{r.Guest.FirstName}</strong>,<br><br>
                    Queremos que su experiencia con nosotros sea perfecta desde el primer momento. Para evitar filas en recepción y agilizar su ingreso al hotel, le invitamos a completar su <strong>Check-in Online</strong>.
                </p>

                <div style='background-color: #fdfbf5; border-radius: 8px; padding: 15px; margin-bottom: 30px; border: 1px solid #f3ebd3;'>
                    <p style='margin: 0; color: #555555; font-size: 15px;'>
                        Confirmación de Reserva:<br>
                        <strong style='color: #D4AF37; font-size: 22px; letter-spacing: 2px; display: inline-block; margin-top: 5px;'>#{r.ConfirmationCode}</strong>
                    </p>
                </div>

                <a href='{link}' style='display: inline-block; background-color: #059669; color: #ffffff; text-decoration: none; padding: 16px 35px; font-size: 16px; font-weight: bold; border-radius: 6px; text-transform: uppercase; letter-spacing: 1px; box-shadow: 0 4px 6px rgba(5, 150, 105, 0.2);'>
                    Hacer Check-in Online
                </a>

                <p style='color: #777777; font-size: 14px; margin-top: 35px; line-height: 1.5;'>
                    En este enlace seguro, podrá confirmar sus datos personales y registrar los documentos de sus acompañantes antes de su llegada.
                </p>
                
                <div style='margin-top: 25px; padding: 15px; background-color: #f5f5f5; border-radius: 6px; text-align: left;'>
                    <p style='color: #999999; font-size: 12px; margin: 0 0 5px 0;'>¿El botón no funciona? Copie y pegue este enlace en su navegador:</p>
                    <a href='{link}' style='color: #059669; font-size: 12px; text-decoration: none; word-break: break-all;'>{link}</a>
                </div>
            </div>

            <div style='background-color: #f9f9f9; padding: 25px 20px; text-align: center; border-top: 1px solid #eeeeee;'>
                <p style='color: #333333; font-weight: bold; margin: 0 0 5px 0; font-size: 14px;'>HOTEL ZAFIRO DORADAL</p>
                <p style='color: #777777; margin: 0 0 15px 0; font-size: 13px;'>+57 3202095352 • zafirohoteldoradal@gmail.com</p>
                <p style='color: #aaaaaa; margin: 0; font-size: 11px; line-height: 1.4;'>Este es un mensaje generado automáticamente.<br>Por favor, no responda a este correo electrónico.</p>
            </div>
        </div>";

        // Asunto más llamativo
        await _emailService.SendEmailAsync(r.Guest.Email, $"⏳ Agilice su llegada: Check-in Online Reserva #{r.ConfirmationCode}", body);
        
        return Ok(new { message = "Enlace de check-in enviado exitosamente." });
    }

    // ==========================================
    // NUEVOS ENDPOINTS: CHECK-IN ONLINE
    // ==========================================

    [HttpGet("by-code/{code}")]
    public async Task<IActionResult> GetReservationByCode(string code)
    {
        var reservation = await _context.Reservations
            .Include(r => r.Guest)
            .FirstOrDefaultAsync(r => r.ConfirmationCode == code || r.Id.ToString() == code);

        if (reservation == null) return NotFound(new { message = "Reserva no encontrada o código inválido." });

        (string p, string s) SplitName(string fullName)
        {
            if (string.IsNullOrWhiteSpace(fullName)) return ("", "");
            var parts = fullName.Trim().Split(' ', 2);
            return (parts[0], parts.Length > 1 ? parts[1] : "");
        }

        var (nom1, nom2) = SplitName(reservation.Guest?.FirstName ?? "");
        var (ape1, ape2) = SplitName(reservation.Guest?.LastName ?? "");

        return Ok(new
        {
            id = reservation.Id,
            confirmationCode = reservation.ConfirmationCode,
            mainGuest = new {
                // Mandamos los campos separados para el frontend
                primerNombre = nom1,
                segundoNombre = nom2,
                primerApellido = ape1,
                segundoApellido = ape2,
                email = reservation.Guest?.Email,
                phone = reservation.Guest?.Phone,
                documentNumber = reservation.Guest?.DocumentNumber,
                documentType = reservation.Guest?.DocumentType.ToString(),
                nationality = reservation.Guest?.Nationality,
                cityOfOrigin = reservation.Guest?.CityOfOrigin,
                // Formateamos la fecha para que el input type="date" lo entienda (yyyy-MM-dd)
                birthDate = reservation.Guest?.BirthDate?.ToString("yyyy-MM-dd") 
            }
        });
    }

    [HttpPost("{id}/online-checkin")]
    public async Task<IActionResult> CompleteOnlineCheckIn(Guid id, [FromBody] OnlineCheckInRequestDto request)
    {
        var reservation = await _context.Reservations
            .Include(r => r.Guest)
            .Include(r => r.ReservationGuests)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (reservation == null) return NotFound(new { message = "Reserva no encontrada." });

        // 1. Actualizar datos del titular
        if (reservation.Guest != null && request.MainGuest != null)
        {
            reservation.Guest.FirstName = $"{request.MainGuest.PrimerNombre} {request.MainGuest.SegundoNombre}".Trim();
            reservation.Guest.LastName = $"{request.MainGuest.PrimerApellido} {request.MainGuest.SegundoApellido}".Trim();
            reservation.Guest.Email = request.MainGuest.Correo;
            reservation.Guest.Phone = request.MainGuest.Telefono;
            reservation.Guest.DocumentNumber = request.MainGuest.NumeroId;
            
            if (Enum.TryParse<IdType>(request.MainGuest.TipoId, out var typeEnum)) 
                reservation.Guest.DocumentType = typeEnum;
                
            reservation.Guest.Nationality = request.MainGuest.Nacionalidad;
            reservation.Guest.CityOfOrigin = request.MainGuest.CiudadOrigen;
            
            if (!string.IsNullOrEmpty(request.MainGuest.FechaCumpleanos))
            {
                var datePart = request.MainGuest.FechaCumpleanos.Split('T')[0]; 
                if (DateOnly.TryParse(datePart, out var bd)) reservation.Guest.BirthDate = bd;
            }
        }

        // 2. Procesar Acompañantes
        if (request.Companions != null && request.Companions.Any())
        {
            // Limpiar acompañantes previos en caso de que lo vuelvan a llenar para corregir algo
            var existingCompanions = reservation.ReservationGuests.Where(rg => !rg.IsPrincipal).ToList();
            _context.ReservationGuests.RemoveRange(existingCompanions);

            foreach (var comp in request.Companions)
            {
                DateOnly? compBirthDate = null;
                if (!string.IsNullOrEmpty(comp.FechaCumpleanos))
                {
                    var datePart = comp.FechaCumpleanos.Split('T')[0];
                    if (DateOnly.TryParse(datePart, out var bd)) compBirthDate = bd;
                }

                var newGuest = new Guest
                {
                    Id = Guid.NewGuid(),
                    FirstName = $"{comp.PrimerNombre} {comp.SegundoNombre}".Trim(),
                    LastName = $"{comp.PrimerApellido} {comp.SegundoApellido}".Trim(),
                    DocumentType = Enum.TryParse<IdType>(comp.TipoId, out var dt) ? dt : IdType.CC,
                    DocumentNumber = comp.NumeroId,
                    Email = comp.Correo ?? "",
                    Phone = comp.Telefono ?? "",
                    Nationality = comp.Nacionalidad ?? "",
                    CityOfOrigin = comp.CiudadOrigen ?? "",
                    BirthDate = compBirthDate,
                    CreatedAt = DateTimeOffset.UtcNow
                };

                _context.Guests.Add(newGuest);

                reservation.ReservationGuests.Add(new ReservationGuest
                {
                    ReservationId = reservation.Id,
                    GuestId = newGuest.Id,
                    IsPrincipal = false
                });
            }
        }

        await _context.SaveChangesAsync();

        // Notificar en la campana al personal del hotel
        await _notificationRepository.AddAsync(
            "Check-in Online",
            $"La reserva {reservation.ConfirmationCode} ha completado su registro previo.",
            NotificationType.Info,
            $"/reservas/{reservation.Id}"
        );

        return Ok(new { message = "Check-in online completado exitosamente." });
    }
}