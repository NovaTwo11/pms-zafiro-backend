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

    public FoliosController(IFolioRepository repository, CashierService cashierService)
    {
        _repository = repository;
        _cashierService = cashierService;
    }

    [HttpGet("active-guests")]
    public async Task<ActionResult<IEnumerable<object>>> GetActiveGuests()
    {
        var folios = await _repository.GetActiveGuestFoliosAsync();
        var result = folios.Select(f => {
            var charges = f.Transactions.Where(t => t.Type == TransactionType.Charge).Sum(t => t.Amount);
            var payments = f.Transactions.Where(t => t.Type == TransactionType.Payment).Sum(t => t.Amount);
            var nights = 0;
            
            if (f.Reservation != null)
            {
                nights = (f.Reservation.CheckOut - f.Reservation.CheckIn).Days;
                if (nights < 1) nights = 1;
            }
            
            return new 
            {
                Id = f.Id,
                Type = "guest",
                Status = f.Status.ToString(),
                Balance = charges - payments,
                GuestName = f.Reservation?.Guest?.FullName ?? "Desconocido",
                RoomNumber = f.Reservation?.Room?.Number ?? "?",
                CheckIn = f.Reservation?.CheckIn,
                CheckOut = f.Reservation?.CheckOut,
                Nights = nights
            };
        });
        return Ok(result);
    }

    [HttpGet("active-externals")]
    public async Task<ActionResult<IEnumerable<object>>> GetActiveExternals()
    {
        var allFolios = await _repository.GetAllAsync(); 
        var externals = allFolios.OfType<ExternalFolio>().Where(f => f.Status == FolioStatus.Open).ToList();
        
        var result = externals.Select(f => {
            var charges = f.Transactions.Where(t => t.Type == TransactionType.Charge).Sum(t => t.Amount);
            var payments = f.Transactions.Where(t => t.Type == TransactionType.Payment).Sum(t => t.Amount);
            return new 
            {
                Id = f.Id,
                Type = "external",
                Status = f.Status.ToString(),
                Balance = charges - payments,
                Alias = f.Alias ?? "Cliente Externo",
                Description = f.Description,
                CreatedAt = f.CreatedAt
            };
        });
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

    // --- MÉTODO CLAVE PARA REPORTES ---
    [HttpPost("{id}/transactions")]
    public async Task<IActionResult> AddTransaction(Guid id, [FromBody] CreateTransactionDto dto)
    {
        // 1. Obtener usuario actual
        string currentUserId = "user1"; 

        // 2. Buscar turno abierto (CRÍTICO)
        // Cualquier movimiento financiero debe quedar auditado en el turno actual.
        var openShift = await _cashierService.GetOpenShiftEntityAsync(currentUserId);
    
        if (openShift == null)
        {
            return BadRequest(new { 
                error = "Caja Cerrada", 
                message = "No hay un turno de caja abierto. Por favor abra la caja antes de registrar movimientos." 
            });
        }

        var folio = await _repository.GetByIdAsync(id);
        if (folio == null) return NotFound("El folio no existe.");
        if (dto.Type == TransactionType.Payment && (int)dto.PaymentMethod < 1)
        {
            return BadRequest("Para registrar un pago debe especificar un método de pago válido (Efectivo, Tarjeta, etc).");
        }

        // 3. Crear la transacción vinculada al turno
        var transaction = new FolioTransaction
        {
            Id = Guid.NewGuid(),
            FolioId = id,
            
            Amount = dto.Amount,
            Description = dto.Description,
            Type = dto.Type, 
            Quantity = dto.Quantity > 0 ? dto.Quantity : 1,
            UnitPrice = dto.UnitPrice > 0 ? dto.UnitPrice : dto.Amount,
            
            
            // Si es un Cargo (consumo), el método de pago es None (0).
            // Si es un Pago (abono), respetamos lo que viene del front (1, 2, 4...).
            PaymentMethod = dto.Type == TransactionType.Charge ? PaymentMethod.None : dto.PaymentMethod,        
            // ¡ESTO ES LO QUE HACE QUE EL REPORTE FUNCIONE!
            CashierShiftId = openShift.Id, 
        
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedByUserId = currentUserId
        };

        // Guardamos usando el método del repositorio que maneja la transacción
        await _repository.AddTransactionAsync(transaction);

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
                PaymentMethod = t.PaymentMethod // Esto devolverá el enum como string o int según configuración JSON
            }).ToList()
        };

        if (folio is GuestFolio guestFolio)
        {
            dto.FolioType = "guest";
            dto.ReservationId = guestFolio.ReservationId; 
            if (guestFolio.Reservation != null)
            {
                dto.GuestName = guestFolio.Reservation.Guest?.FullName ?? "Huésped";
                dto.RoomNumber = guestFolio.Reservation.Room?.Number ?? "N/A";
                dto.CheckIn = guestFolio.Reservation.CheckIn;
                dto.CheckOut = guestFolio.Reservation.CheckOut;
                
                var nights = (guestFolio.Reservation.CheckOut - guestFolio.Reservation.CheckIn).Days;
                dto.Nights = nights < 1 ? 1 : nights;
            }
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