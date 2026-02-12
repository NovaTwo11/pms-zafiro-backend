using Microsoft.AspNetCore.Mvc;
using PmsZafiro.Application.DTOs.Folios;
using PmsZafiro.Application.Interfaces;
using PmsZafiro.Application.Services; // Importar Servicio
using PmsZafiro.Domain.Entities;
using PmsZafiro.Domain.Enums;

namespace PmsZafiro.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class FoliosController : ControllerBase
{
    private readonly IFolioRepository _repository;
    private readonly CashierService _cashierService; // Inyección

    // Constructor actualizado
    public FoliosController(IFolioRepository repository, CashierService cashierService)
    {
        _repository = repository;
        _cashierService = cashierService;
    }

    // ... (Métodos GET activos se mantienen igual) ...
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
            CreatedAt = DateTime.UtcNow
        };
        await _repository.AddAsync(folio);
        return CreatedAtAction(nameof(GetById), new { id = folio.Id }, new { id = folio.Id, message = "Folio externo creado" });
    }

    // --- AQUÍ ESTÁ EL CAMBIO IMPORTANTE ---
    [HttpPost("{id}/transactions")]
    public async Task<IActionResult> AddTransaction(Guid id, [FromBody] CreateTransactionDto dto)
    {
        // 1. OBTENER USUARIO ACTUAL (Hardcodeado temporalmente o por Token)
        string currentUserId = "user1"; 

        // 2. BUSCAR TURNO ABIERTO (CRÍTICO: Esto faltaba o fallaba)
        var openShift = await _cashierService.GetOpenShiftEntityAsync(currentUserId); // Necesitamos exponer este método o usar el Repo directo
    
        if (openShift == null)
        {
            return BadRequest(new { 
                error = "Caja Cerrada", 
                message = "No se pueden procesar pagos sin un turno de caja abierto." 
            });
        }

        var folio = await _repository.GetByIdAsync(id);
        if (folio == null) return NotFound("El folio no existe.");

        // 3. VINCULACIÓN AUTOMÁTICA
        var transaction = new FolioTransaction
        {
            FolioId = id,
            Amount = dto.Amount,
            Description = dto.Description,
            Type = dto.Type, 
            Quantity = dto.Quantity,
            UnitPrice = dto.UnitPrice > 0 ? dto.UnitPrice : dto.Amount,
            PaymentMethod = dto.PaymentMethod,
        
            // ¡ESTA ES LA LÍNEA CLAVE!
            CashierShiftId = openShift.Id, 
        
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedByUserId = currentUserId
        };

        await _repository.AddTransactionAsync(transaction);

        return Ok(new { message = "Transacción exitosa", transactionId = transaction.Id });
    }

    private FolioDto MapToDto(Folio folio)
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