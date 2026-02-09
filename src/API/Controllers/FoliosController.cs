using Microsoft.AspNetCore.Mvc;
using PmsZafiro.Application.DTOs.Folios;
using PmsZafiro.Application.Interfaces;
using PmsZafiro.Domain.Entities;
using PmsZafiro.Domain.Enums;

namespace PmsZafiro.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class FoliosController : ControllerBase
{
    private readonly IFolioRepository _repository;

    public FoliosController(IFolioRepository repository)
    {
        _repository = repository;
    }

    // 1. Endpoint Existente: Huéspedes
    [HttpGet("active-guests")]
    public async Task<ActionResult<IEnumerable<object>>> GetActiveGuests()
    {
        var folios = await _repository.GetActiveGuestFoliosAsync();
        
        var result = folios.Select(f => {
            var charges = f.Transactions.Where(t => t.Type == TransactionType.Charge).Sum(t => t.Amount);
            var payments = f.Transactions.Where(t => t.Type == TransactionType.Payment).Sum(t => t.Amount);
            
            var nights = 0;
            if (f.Reservation != null) // Validación de seguridad
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

    // 2. NUEVO ENDPOINT: Externos / Pasadías
    // Soluciona el error 400 al evitar que caiga en el endpoint {id}
    [HttpGet("active-externals")]
    public async Task<ActionResult<IEnumerable<object>>> GetActiveExternals()
    {
        // Nota: Asegúrate de tener este método en tu IFolioRepository o usa GetAll y filtra aquí
        // Si no lo tienes en el repo, avísame para darte el código del repositorio.
        // Asumiremos por ahora que puedes filtrar o que agregarás el método.
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

    // 3. NUEVO ENDPOINT: Crear Folio Externo
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

        await _repository.AddAsync(folio); // Asegúrate que tu repositorio tenga AddAsync genérico o para Folio

        return CreatedAtAction(nameof(GetById), new { id = folio.Id }, new { id = folio.Id, message = "Folio externo creado" });
    }

    [HttpPost("{id}/transactions")]
    public async Task<IActionResult> AddTransaction(Guid id, [FromBody] CreateTransactionDto dto)
    {
        var folio = await _repository.GetByIdAsync(id);
        if (folio == null) return NotFound();

        var transaction = new FolioTransaction
        {
            FolioId = id,
            Amount = dto.Amount,
            Description = dto.Description,
            Type = dto.Type, 
            Quantity = dto.Quantity,
            UnitPrice = dto.UnitPrice > 0 ? dto.UnitPrice : dto.Amount,
            PaymentMethod = dto.PaymentMethod,
            CashierShiftId = dto.CashierShiftId,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedByUserId = "POS-USER" 
        };

        await _repository.AddTransactionAsync(transaction);

        return Ok(new { message = "Transacción agregada", transactionId = transaction.Id });
    }

    private FolioDto MapToDto(Folio folio)
    {
        var charges = folio.Transactions.Where(t => t.Type == TransactionType.Charge).Sum(t => t.Amount);
        var payments = folio.Transactions.Where(t => t.Type == TransactionType.Payment).Sum(t => t.Amount);
        
        var dto = new FolioDto
        {
            Id = folio.Id,
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
                PaymentMethod = t.PaymentMethod // Agregamos esto si lo tienes en el DTO para visualización
            }).ToList()
        };

        // Mapeo condicional según el tipo
        if (folio is GuestFolio guestFolio)
        {
            dto.ReservationId = guestFolio.ReservationId;
            dto.GuestName = guestFolio.Reservation?.Guest?.FullName;
            dto.RoomNumber = guestFolio.Reservation?.Room?.Number;
        }
        else if (folio is ExternalFolio externalFolio)
        {
            dto.Alias = externalFolio.Alias;
            dto.Description = externalFolio.Description;
        }

        return dto;
    }
}