# fix_folios_syntax.ps1

$CurrentLocation = Get-Location
$SrcPath = Join-Path $CurrentLocation "src"

if (-not (Test-Path $SrcPath)) { Write-Error "❌ Ejecuta esto en la raíz del proyecto."; exit }

$DtoPath = Join-Path $SrcPath "Application/DTOs/Folios/CreateTransactionDto.cs"
$ControllerPath = Join-Path $SrcPath "API/Controllers/FoliosController.cs"

# ---------------------------------------------------------
# 1. REPARAR DTO (CreateTransactionDto.cs)
# Usamos @' para strings literales (sin escapar comillas)
# ---------------------------------------------------------
$DtoContent = @'
using System.ComponentModel.DataAnnotations;
using PmsZafiro.Domain.Enums;

namespace PmsZafiro.Application.DTOs.Folios;

public class CreateTransactionDto
{
    [Required] public decimal Amount { get; set; }
    [Required] public string Description { get; set; } = string.Empty;
    [Required] public TransactionType Type { get; set; } 
    public int Quantity { get; set; } = 1;
    public decimal UnitPrice { get; set; }
    
    // Propiedades para POS
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.None;
    public Guid? CashierShiftId { get; set; }
    public string? Category { get; set; } // Se recibe del front pero no se guarda si la entidad no lo tiene
}
'@

Set-Content -Path $DtoPath -Value $DtoContent
Write-Host "✅ CreateTransactionDto.cs reparado." -ForegroundColor Green

# ---------------------------------------------------------
# 2. REPARAR CONTROLADOR (FoliosController.cs)
# Eliminamos la asignación a 'Category' que causaba error CS0117
# ---------------------------------------------------------
$ControllerContent = @'
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
                GuestName = f.Reservation.Guest?.FullName ?? "Desconocido",
                RoomNumber = f.Reservation.Room?.Number ?? "?",
                CheckIn = f.Reservation.CheckIn,
                CheckOut = f.Reservation.CheckOut,
                Nights = nights
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
            // Eliminamos Category porque la entidad FolioTransaction no la tiene
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
'@

Set-Content -Path $ControllerPath -Value $ControllerContent
Write-Host "✅ FoliosController.cs reparado sin errores de sintaxis." -ForegroundColor Green
Write-Host "🚀 Ejecuta 'dotnet run --project src/API/PmsZafiro.API.csproj' ahora." -ForegroundColor Cyan