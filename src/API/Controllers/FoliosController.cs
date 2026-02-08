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

    // GET api/folios/reservation/{id}
    [HttpGet("reservation/{reservationId}")]
    public async Task<ActionResult<FolioDto>> GetByReservation(Guid reservationId)
    {
        var folio = await _repository.GetByReservationIdAsync(reservationId);
        if (folio == null) return NotFound("No se encontró folio para esta reserva");

        return Ok(MapToDto(folio));
    }

    // GET api/folios/{id}
    [HttpGet("{id}")]
    public async Task<ActionResult<FolioDto>> GetById(Guid id)
    {
        var folio = await _repository.GetByIdAsync(id);
        if (folio == null) return NotFound();

        return Ok(MapToDto(folio));
    }

    // POST api/folios/{id}/transactions
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
            CreatedByUserId = "Admin" // En el futuro vendrá del Token JWT
        };

        await _repository.AddTransactionAsync(transaction);

        return Ok(new { message = "Transacción agregada", transactionId = transaction.Id });
    }

    // Método auxiliar para convertir Entidad -> DTO y calcular saldos
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
            Transactions = folio.Transactions.Select(t => new FolioTransactionDto
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
