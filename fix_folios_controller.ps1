# fix_folios_controller.ps1

$CurrentLocation = Get-Location
$SrcPath = Join-Path $CurrentLocation "src"
$ControllerPath = Join-Path $SrcPath "API/Controllers/FoliosController.cs"

if (-not (Test-Path $SrcPath)) { Write-Error "❌ Ejecuta esto en la raíz del proyecto."; exit }

$Content = @"
using Microsoft.AspNetCore.Mvc;
using PmsZafiro.Application.DTOs.Folios;
using PmsZafiro.Application.Interfaces;
using PmsZafiro.Domain.Entities;
using PmsZafiro.Domain.Enums;

namespace PmsZafiro.API.Controllers;

[ApiController]
[Route(""api/[controller]"")]
public class FoliosController : ControllerBase
{
    private readonly IFolioRepository _repository;

    public FoliosController(IFolioRepository repository)
    {
        _repository = repository;
    }

    [HttpGet(""active-guests"")]
    public async Task<ActionResult<IEnumerable<object>>> GetActiveGuests()
    {
        var folios = await _repository.GetActiveGuestFoliosAsync();
        
        var result = folios.Select(f => {
            var charges = f.Transactions.Where(t => t.Type == TransactionType.Charge).Sum(t => t.Amount);
            var payments = f.Transactions.Where(t => t.Type == TransactionType.Payment).Sum(t => t.Amount);
            
            var nights = (f.Reservation.CheckOut - f.Reservation.CheckIn).Days;
            if (nights < 1) nights = 1;

            return new 
            {
                Id = f.Id,
                Status = f.Status.ToString(),
                Balance = charges - payments,
                GuestName = f.Reservation.Guest?.FullName ?? ""Desconocido"",
                RoomNumber = f.Reservation.Room?.Number ?? ""?"",
                CheckIn = f.Reservation.CheckIn,
                CheckOut = f.Reservation.CheckOut,
                Nights = nights
            };
        });
        
        return Ok(result);
    }

    [HttpGet(""{id}"")]
    public async Task<ActionResult<FolioDto>> GetById(Guid id)
    {
        var folio = await _repository.GetByIdAsync(id);
        if (folio == null) return NotFound();

        return Ok(MapToDto(folio));
    }

    [HttpPost(""{id}/transactions"")]
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
            Category = dto.Category ?? ""POS"", // Categoría por defecto
            PaymentMethod = dto.PaymentMethod,
            CashierShiftId = dto.CashierShiftId, // ✅ CRÍTICO: Guardar el turno
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedByUserId = ""POS-USER"" 
        };

        await _repository.AddTransactionAsync(transaction);

        return Ok(new { message = ""Transacción agregada"", transactionId = transaction.Id });
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
                Date = t.CreatedAt.ToString(""yyyy-MM-dd HH:mm""),
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
"@

Set-Content -Path $ControllerPath -Value $Content
Write-Host "✅ FoliosController actualizado con soporte para CashierShiftId." -ForegroundColor Green
Write-Host "🔄 Reinicia el backend con 'dotnet run'." -ForegroundColor Cyan