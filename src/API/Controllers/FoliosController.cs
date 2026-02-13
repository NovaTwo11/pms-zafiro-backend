using Microsoft.AspNetCore.Mvc;
using PmsZafiro.Application.DTOs.Folios;
using PmsZafiro.Application.Interfaces;
using PmsZafiro.Application.Services;
using PmsZafiro.Domain.Entities;
using PmsZafiro.Domain.Enums;

namespace PmsZafiro.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class FoliosController : ControllerBase
{
    private readonly IFolioRepository _repository;
    private readonly CashierService _cashierService;
    private readonly IProductRepository _productRepository;

    public FoliosController(IFolioRepository repository, CashierService cashierService, IProductRepository productRepository)
    {
        _repository = repository;
        _cashierService = cashierService;
        _productRepository = productRepository;
    }
    
    [HttpGet("active-guests")]
    public async Task<ActionResult<IEnumerable<object>>> GetActiveGuests()
    {
        // 1. Traer datos
        var folios = await _repository.GetActiveGuestFoliosAsync();
        var result = new List<object>();

        // 2. Iteración SECUENCIAL (Fix para evitar error de concurrencia de DbContext)
        foreach (var f in folios)
        {
            // Consulta segura al repo (una por una)
            var dbBalance = await _repository.GetFolioBalanceAsync(f.Id);

            var nights = 0;
            string roomNumber = "?";
            DateTime? checkIn = null;
            DateTime? checkOut = null;
            string guestName = "Desconocido";

            if (f.Reservation != null)
            {
                guestName = f.Reservation.Guest?.FullName ?? "Desconocido";
                checkIn = f.Reservation.CheckIn;
                checkOut = f.Reservation.CheckOut;
            
                nights = (f.Reservation.CheckOut - f.Reservation.CheckIn).Days;
                if (nights < 1) nights = 1;

                // Null safety para segmentos
                var segments = f.Reservation.Segments;
                if (segments != null && segments.Any())
                {
                    var activeSegment = segments
                                            .OrderBy(s => s.CheckIn)
                                            .FirstOrDefault(s => s.CheckIn <= DateTime.UtcNow && s.CheckOut > DateTime.UtcNow) 
                                        ?? segments.FirstOrDefault();

                    roomNumber = activeSegment?.Room?.Number ?? "?";
                }
            }

            result.Add(new 
            {
                Id = f.Id,
                Type = "guest",
                Status = f.Status.ToString(),
                Balance = dbBalance, 
                GuestName = guestName,
                RoomNumber = roomNumber,
                CheckIn = checkIn,
                CheckOut = checkOut,
                Nights = nights
            });
        }
    
        return Ok(result);
    }

    [HttpGet("active-externals")]
    public async Task<ActionResult<IEnumerable<object>>> GetActiveExternals()
    {
        var allFolios = await _repository.GetAllAsync(); 
        var externals = allFolios.OfType<ExternalFolio>().Where(f => f.Status == FolioStatus.Open).ToList();
        var result = new List<object>();

        // Fix Concurrencia aquí también
        foreach (var f in externals)
        {
            var dbBalance = await _repository.GetFolioBalanceAsync(f.Id);
            result.Add(new 
            {
                Id = f.Id,
                Type = "external",
                Status = f.Status.ToString(),
                Balance = dbBalance,
                Alias = f.Alias ?? "Cliente Externo",
                Description = f.Description,
                CreatedAt = f.CreatedAt
            });
        }
        
        return Ok(result);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<FolioDto>> GetById(Guid id)
    {
        var folio = await _repository.GetByIdAsync(id);
        if (folio == null) return NotFound();
        return Ok(MapToDto(folio));
    }

    [HttpPost("external")]
    public async Task<IActionResult> CreateExternalFolio([FromBody] CreateExternalFolioDto dto)
    {
        var folio = new ExternalFolio
        {
            Alias = dto.Alias,
            Description = dto.Description,
            Status = FolioStatus.Open,
            CreatedAt = DateTimeOffset.UtcNow
        };
        await _repository.AddAsync(folio);
        return CreatedAtAction(nameof(GetById), new { id = folio.Id }, new { id = folio.Id, message = "Folio externo creado" });
    }

    [HttpPost("{id}/transactions")]
    public async Task<IActionResult> AddTransaction(Guid id, [FromBody] CreateTransactionDto dto)
    {
        // 1. Obtener usuario actual
        string currentUserId = "user1"; 

        // 2. Buscar turno abierto
        var openShift = await _cashierService.GetOpenShiftEntityAsync(currentUserId);
    
        if (openShift == null)
        {
            return BadRequest(new { 
                error = "Caja Cerrada", 
                message = "No hay un turno de caja abierto. Por favor abra la caja antes de registrar movimientos." 
            });
        }

        // VALIDACIÓN ANTI-ERROR CONTABLE
        if (dto.Amount < 0 && dto.Type != TransactionType.Charge)
        {
             return BadRequest(new { 
                error = "Operación Inválida", 
                message = "Solo los Cargos pueden tener valor negativo (para correcciones). Los Pagos deben ser positivos." 
            });
        }

        var folio = await _repository.GetByIdAsync(id);
        if (folio == null) return NotFound("El folio no existe.");
        
        if (dto.Type == TransactionType.Payment && (int)dto.PaymentMethod < 1)
        {
            return BadRequest("Para registrar un pago debe especificar un método de pago válido.");
        }

        // 3. Crear la transacción
        var transaction = new FolioTransaction
        {
            Id = Guid.NewGuid(),
            FolioId = id,
            Amount = dto.Amount,
            Description = dto.Description,
            Type = dto.Type, 
            Quantity = dto.Quantity > 0 ? dto.Quantity : 1,
            UnitPrice = dto.UnitPrice > 0 ? dto.UnitPrice : (dto.Amount < 0 ? -dto.Amount : dto.Amount),
            PaymentMethod = dto.Type == TransactionType.Charge ? PaymentMethod.None : dto.PaymentMethod,        
            CashierShiftId = openShift.Id,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedByUserId = currentUserId
        };

        await _repository.AddTransactionAsync(transaction);
        
        if (dto.ProductId.HasValue && dto.Type == TransactionType.Charge)
        {
            var product = await _productRepository.GetByIdAsync(dto.ProductId.Value);
            if (product != null && product.IsStockTracked)
            {
                product.Stock -= dto.Quantity;
                // Si permites vender en negativo, omite la siguiente línea. Si no, déjala:
                if (product.Stock < 0) product.Stock = 0; 

                await _productRepository.UpdateAsync(product);
            }
        }

        return Ok(new { message = "Transacción registrada correctamente", transactionId = transaction.Id });
    }

    private static FolioDto MapToDto(Folio folio)
    {
        var charges = folio.Transactions.Where(t => t.Type == TransactionType.Charge).Sum(t => t.Amount);
        var payments = folio.Transactions.Where(t => t.Type == TransactionType.Payment).Sum(t => t.Amount);
        
        var dto = new FolioDto
        {
            Id = folio.Id,
            CreatedAt = folio.CreatedAt.DateTime, 
            Status = folio.Status.ToString(),
            Balance = charges - payments,
            TotalCharges = charges,
            TotalPayments = payments,
            
            Transactions = folio.Transactions.OrderByDescending(t => t.CreatedAt).Select(t => new FolioTransactionDto
            {
                Id = t.Id,
                Date = t.CreatedAt.ToString("yyyy-MM-dd HH:mm"),
                Description = t.Description,
                Amount = t.Amount,
                Type = t.Type.ToString(),
                UnitPrice = t.UnitPrice,
                Quantity = t.Quantity,
                User = t.CreatedByUserId,
                PaymentMethod = t.PaymentMethod 
            }).ToList()
        };

        if (folio is GuestFolio guestFolio && guestFolio.Reservation != null)
        {
            dto.FolioType = "guest";
            dto.ReservationId = guestFolio.ReservationId;
            
            var segments = guestFolio.Reservation.Segments;
            var activeSegment = (segments != null && segments.Any())
                ? segments.OrderBy(s => s.CheckIn)
                          .FirstOrDefault(s => s.CheckIn <= DateTime.UtcNow && s.CheckOut > DateTime.UtcNow) 
                  ?? segments.FirstOrDefault()
                : null;

            dto.GuestName = guestFolio.Reservation.Guest?.FullName ?? "Huésped";
            dto.RoomNumber = activeSegment?.Room?.Number ?? "N/A"; 
            dto.CheckIn = guestFolio.Reservation.CheckIn;
            dto.CheckOut = guestFolio.Reservation.CheckOut;
            
            var nights = (guestFolio.Reservation.CheckOut - guestFolio.Reservation.CheckIn).Days;
            dto.Nights = nights < 1 ? 1 : nights;
        }
        else if (folio is ExternalFolio externalFolio)
        {
            dto.FolioType = "external";
            dto.Alias = externalFolio.Alias;
            dto.Description = externalFolio.Description;
        }
        
        return dto;
    }
}