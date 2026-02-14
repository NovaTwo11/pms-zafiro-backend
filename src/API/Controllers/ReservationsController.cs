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
            
        var balance = charges - paidAmount;

        // --- Helper Local para Nombres ---
        // Separa "Juan Carlos" en "Juan" y "Carlos" para que el frontend no se rompa
        (string p, string s) SplitName(string fullName)
        {
            if (string.IsNullOrWhiteSpace(fullName)) return ("", "");
            var parts = fullName.Trim().Split(' ', 2);
            return (parts[0], parts.Length > 1 ? parts[1] : "");
        }

        var guestsList = new List<GuestDetailDto>();

        // 1. Mapeo del Titular (Datos Completos)
        if (r.Guest != null)
        {
            var (nom1, nom2) = SplitName(r.Guest.FirstName);
            var (ape1, ape2) = SplitName(r.Guest.LastName);
            
            // Detectar firma en las notas
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

        // 2. Mapeo de Acompañantes (Datos Básicos)
        if (r.ReservationGuests != null)
        {
            foreach (var rg in r.ReservationGuests)
            {
                if (rg.Guest == null) continue;
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
                    IsSigned = true // Acompañantes asumen estado OK por simplicidad
                });
            }
        } 

        // --- Construcción del DTO Final ---
        var mainSegment = r.Segments.OrderBy(s => s.CheckIn).FirstOrDefault();
        
        // Calcular Status Step visual
        int statusStep = r.Status switch
        {
            ReservationStatus.Confirmed => 2,
            ReservationStatus.CheckedIn => 3,
            ReservationStatus.CheckedOut => 4,
            ReservationStatus.Cancelled or ReservationStatus.NoShow => 0,
            _ => 1 // Pending
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
            
            Guests = guestsList, // ¡Lista corregida!
            
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

    // ==========================================
    // CREATE: RESERVA MANUAL (CORREGIDO)
    // ==========================================
    [HttpPost]
    public async Task<ActionResult<ReservationDto>> Create(CreateReservationDto dto)
    {
        // A. Validar Habitación y Calcular Precios
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

        // B. Manejo de Huésped (Upsert y Bloqueos)
        Guest? guest = null;

        // Caso Especial: BLOQUEO o MANTENIMIENTO
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
            // 1. Intentar buscar por ID si viene
            if (!string.IsNullOrEmpty(dto.MainGuestId) && Guid.TryParse(dto.MainGuestId, out Guid guestId))
            {
                guest = await _guestRepository.GetByIdAsync(guestId);
            }
            
            // 2. Si no, intentar buscar por Documento (Evitar duplicados)
            if (guest == null && !string.IsNullOrEmpty(dto.GuestDocNumber))
            {
                guest = await _guestRepository.GetByDocumentAsync(dto.GuestDocNumber);
            }
            
            // 3. Si sigue siendo null, CREARLO con todos los datos
            if (guest == null)
            {
                // Parseo de fecha seguro (el código de arriba)
                DateOnly? birthDateParsed = null;
                if (!string.IsNullOrEmpty(dto.GuestBirthDate))
                {
                    var datePart = dto.GuestBirthDate.Split('T')[0]; 
                    DateOnly.TryParse(datePart, out var bd);
                    birthDateParsed = bd;
                }

                guest = new Guest
                {
                    // Usamos los campos específicos que enviaste desde el modal
                    FirstName = $"{dto.GuestFirstName} {dto.GuestSecondName}".Trim(),
                    LastName = $"{dto.GuestLastName} {dto.GuestSecondLastName}".Trim(),
        
                    DocumentType = Enum.TryParse<IdType>(dto.GuestDocType, out var dt) ? dt : IdType.CC,
                    DocumentNumber = !string.IsNullOrEmpty(dto.GuestDocNumber) ? dto.GuestDocNumber : "PEND-" + Guid.NewGuid().ToString()[..4],
        
                    Email = dto.GuestEmail ?? "",
                    Phone = dto.GuestPhone ?? "",
                    Nationality = "Colombia", // O recibirlo del DTO
                    BirthDate = birthDateParsed,
                    CityOfOrigin = dto.GuestCityOrigin,
                    
                    CreatedAt = DateTimeOffset.UtcNow
                };
                await _guestRepository.AddAsync(guest);
            }
            else 
            {
                // Opcional: Actualizar datos si el huésped ya existía pero queremos refrescar info
                if (!string.IsNullOrEmpty(dto.GuestEmail)) guest.Email = dto.GuestEmail;
                if (!string.IsNullOrEmpty(dto.GuestPhone)) guest.Phone = dto.GuestPhone;
                await _context.SaveChangesAsync();
            }
        }

        // C. Crear Reserva
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

        // D. Notificar
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
        var reservation = await _context.Reservations
            .Include(r => r.Guest)
            .Include(r => r.ReservationGuests).ThenInclude(rg => rg.Guest)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (reservation == null) return NotFound(new { message = "Reserva no encontrada" });
        if (reservation.Guest == null) return NotFound(new { message = "El huésped titular no existe (Error de integridad)." });

        // 1. ACTUALIZAR TITULAR (Merge de datos)
        // Combinamos Primer y Segundo nombre/apellido para guardar en el campo único de BD
        var g = reservation.Guest;
        g.FirstName = $"{dto.PrimerNombre} {dto.SegundoNombre}".Trim(); 
        g.LastName = $"{dto.PrimerApellido} {dto.SegundoApellido}".Trim();
        g.DocumentNumber = dto.NumeroId;
        g.Nationality = dto.Nacionalidad;
        
        // Actualizar solo si hay datos nuevos (no borrar lo existente si viene nulo)
        if (!string.IsNullOrEmpty(dto.Telefono)) g.Phone = dto.Telefono;
        if (!string.IsNullOrEmpty(dto.Correo)) g.Email = dto.Correo;
        if (Enum.TryParse<IdType>(dto.TipoId, out var typeEnum)) g.DocumentType = typeEnum;

        // 2. FIRMA DIGITAL
        if (!string.IsNullOrEmpty(dto.SignatureBase64))
        {
            if (reservation.Notes == null) reservation.Notes = "";
            if (!reservation.Notes.Contains("[FIRMADO]"))
            {
                reservation.Notes += $" [FIRMADO: {DateTime.UtcNow:dd/MM/yyyy HH:mm}]";
            }
        }

        // 3. GESTIÓN DE ACOMPAÑANTES (Full Sync)
        if (dto.Companions != null)
        {
            // Limpiar lista actual para evitar duplicados en la relación
            if (reservation.ReservationGuests.Any())
            {
                _context.ReservationGuests.RemoveRange(reservation.ReservationGuests);
            }

            foreach (var compDto in dto.Companions)
            {
                if (string.IsNullOrWhiteSpace(compDto.NumeroId)) continue;

                // Lógica UPSERT para Acompañante
                var existingGuest = await _context.Guests
                    .FirstOrDefaultAsync(g => g.DocumentNumber == compDto.NumeroId);

                if (existingGuest == null)
                {
                    existingGuest = new Guest
                    {
                        FirstName = $"{compDto.PrimerNombre} {compDto.SegundoNombre}".Trim(),
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
                    // Si ya existe, actualizamos sus nombres para corregir errores tipográficos
                    existingGuest.FirstName = $"{compDto.PrimerNombre} {compDto.SegundoNombre}".Trim();
                    existingGuest.LastName = $"{compDto.PrimerApellido} {compDto.SegundoApellido}".Trim();
                    if (!string.IsNullOrEmpty(compDto.Nacionalidad)) existingGuest.Nationality = compDto.Nacionalidad;
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
}