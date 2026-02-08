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

    [HttpGet("active-guests")]
    public async Task<ActionResult<IEnumerable<object>>> GetActiveGuests()
    {
        var folios = await _repository.GetActiveGuestFoliosAsync();
        
        // Corrección: Usamos CheckIn/CheckOut en lugar de StartDate/EndDate
        var result = folios.Select(f => {
            var charges = f.Transactions.Where(t => t.Type == TransactionType.Charge).Sum(t => t.Amount);
            var payments = f.Transactions.Where(t => t.Type == TransactionType.Payment).Sum(t => t.Amount);
            
            // Calculamos noches de forma segura
            var nights = (f.Reservation.CheckOut - f.Reservation.CheckIn).Days;
            if (nights < 1) nights = 1;

            return new 
            {
                Id = f.Id,
                Status = f.Status.ToString(),
                Balance = charges - payments,
                GuestName = f.Reservation.Guest?.FullName ?? "Desconocido",
                RoomNumber = f.Reservation.Room?.Number ?? "?",
                CheckIn = f.Reservation.CheckIn,   // ✅ Corregido
                CheckOut = f.Reservation.CheckOut, // ✅ Corregido
                Nights = nights
            };
        });
        
        return Ok(result);
    }

    [HttpGet("active-externals")]
    public async Task<ActionResult<IEnumerable<object>>> GetActiveExternals()
    {
        var folios = await _repository.GetActiveExternalFoliosAsync();
        var result = folios.Select(f => {
            var charges = f.Transactions.Where(t => t.Type == TransactionType.Charge).Sum(t => t.Amount);
            var payments = f.Transactions.Where(t => t.Type == TransactionType.Payment).Sum(t => t.Amount);
            return new 
            {
                Id = f.Id,
                Status = f.Status.ToString(),
                Balance = charges - payments,
                Alias = f.Alias,
                Description = f.Description,
                CreatedAt = DateTime.Now // Ajustar si agregas propiedad CreatedAt a Folio
            };
        });
        return Ok(result);
    }

    [HttpPost("external")]
    public async Task<IActionResult> CreateExternal([FromBody] CreateExternalFolioDto dto)
    {
        var folio = new ExternalFolio
        {
            Alias = dto.Alias,
            Description = dto.Description,
            Status = FolioStatus.Open
        };
        
        await _repository.CreateAsync(folio);
        return Ok(new { id = folio.Id, message = "Folio externo creado" });
    }

    [HttpGet("reservation/{reservationId}")]
    public async Task<ActionResult<FolioDto>> GetByReservation(Guid reservationId)
    {
        var folio = await _repository.GetByReservationIdAsync(reservationId);
        if (folio == null) return NotFound("No se encontró folio para esta reserva");

        return Ok(MapToDto(folio));
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<FolioDto>> GetById(Guid id)
    {
        var folio = await _repository.GetByIdAsync(id);
        if (folio == null) return NotFound();

        return Ok(MapToDto(folio));
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
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedByUserId = "Admin" // TODO: Obtener del contexto de usuario real
        };

        await _repository.AddTransactionAsync(transaction);

        return Ok(new { message = "Transacción agregada", transactionId = transaction.Id });
    }

    private FolioDto MapToDto(Folio folio)
    {
        var charges = folio.Transactions.Where(t => t.Type == TransactionType.Charge).Sum(t => t.Amount);
        var payments = folio.Transactions.Where(t => t.Type == TransactionType.Payment).Sum(t => t.Amount);
        
        return new FolioDto
        {
            Id = folio.Id,
            ReservationId = (folio as GuestFolio)?.ReservationId,
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
                User = t.CreatedByUserId
            }).ToList()
        };
    }
}