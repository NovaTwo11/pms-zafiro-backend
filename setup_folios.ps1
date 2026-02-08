$SolutionName = "PmsZafiro"
$BaseDir = Get-Location

Write-Host "Generando Módulo de Folios (Cuentas)..." -ForegroundColor Cyan

# --- CARPETAS ---
$DtoPath = "$BaseDir/src/Application/DTOs/Folios"
$InterfacePath = "$BaseDir/src/Application/Interfaces"
$RepoPath = "$BaseDir/src/Infrastructure/Repositories"
$ControllerPath = "$BaseDir/src/API/Controllers"

New-Item -ItemType Directory -Force -Path $DtoPath | Out-Null

# --- DTOs ---
# Detalle de una transacción (fila de la factura)
$ContentTransDto = @"
namespace $SolutionName.Application.DTOs.Folios;

public class FolioTransactionDto
{
    public Guid Id { get; set; }
    public string Date { get; set; } = string.Empty; // Formateada
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Type { get; set; } = string.Empty; // Charge, Payment
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
    public string User { get; set; } = string.Empty;
}
"@
Set-Content -Path "$DtoPath/FolioTransactionDto.cs" -Value $ContentTransDto

# El Folio completo con saldo
$ContentFolioDto = @"
namespace $SolutionName.Application.DTOs.Folios;

public class FolioDto
{
    public Guid Id { get; set; }
    public Guid? ReservationId { get; set; }
    public string Status { get; set; } = string.Empty;
    
    public decimal Balance { get; set; } // Calculado
    public decimal TotalCharges { get; set; }
    public decimal TotalPayments { get; set; }
    
    public List<FolioTransactionDto> Transactions { get; set; } = new();
}
"@
Set-Content -Path "$DtoPath/FolioDto.cs" -Value $ContentFolioDto

# DTO para crear un movimiento (Cobrar o Pagar)
$ContentCreateTransDto = @"
using System.ComponentModel.DataAnnotations;
using $SolutionName.Domain.Enums;

namespace $SolutionName.Application.DTOs.Folios;

public class CreateTransactionDto
{
    [Required] public decimal Amount { get; set; }
    [Required] public string Description { get; set; } = string.Empty;
    [Required] public TransactionType Type { get; set; } // 0=Charge, 1=Payment
    public int Quantity { get; set; } = 1;
    public decimal UnitPrice { get; set; }
}
"@
Set-Content -Path "$DtoPath/CreateTransactionDto.cs" -Value $ContentCreateTransDto

# --- INTERFAZ ---
$ContentIRepo = @"
using $SolutionName.Domain.Entities;

namespace $SolutionName.Application.Interfaces;

public interface IFolioRepository
{
    Task<Folio?> GetByIdAsync(Guid id);
    Task<Folio?> GetByReservationIdAsync(Guid reservationId);
    Task AddTransactionAsync(FolioTransaction transaction);
}
"@
Set-Content -Path "$InterfacePath/IFolioRepository.cs" -Value $ContentIRepo

# --- REPOSITORIO ---
$ContentRepo = @"
using Microsoft.EntityFrameworkCore;
using $SolutionName.Application.Interfaces;
using $SolutionName.Domain.Entities;
using $SolutionName.Infrastructure.Persistence;

namespace $SolutionName.Infrastructure.Repositories;

public class FolioRepository : IFolioRepository
{
    private readonly PmsDbContext _context;

    public FolioRepository(PmsDbContext context)
    {
        _context = context;
    }

    public async Task<Folio?> GetByIdAsync(Guid id)
    {
        return await _context.Folios
            .Include(f => f.Transactions.OrderByDescending(t => t.CreatedAt))
            .FirstOrDefaultAsync(f => f.Id == id);
    }

    public async Task<Folio?> GetByReservationIdAsync(Guid reservationId)
    {
        // Buscamos el GuestFolio asociado a esa reserva
        return await _context.Folios
            .OfType<GuestFolio>() // Filtramos solo folios de huésped
            .Include(f => f.Transactions.OrderByDescending(t => t.CreatedAt))
            .FirstOrDefaultAsync(f => f.ReservationId == reservationId);
    }

    public async Task AddTransactionAsync(FolioTransaction transaction)
    {
        _context.Transactions.Add(transaction);
        await _context.SaveChangesAsync();
    }
}
"@
Set-Content -Path "$RepoPath/FolioRepository.cs" -Value $ContentRepo

# --- CONTROLLER ---
$ContentController = @"
using Microsoft.AspNetCore.Mvc;
using $SolutionName.Application.DTOs.Folios;
using $SolutionName.Application.Interfaces;
using $SolutionName.Domain.Entities;
using $SolutionName.Domain.Enums;

namespace $SolutionName.API.Controllers;

[ApiController]
[Route(""api/[controller]"")]
public class FoliosController : ControllerBase
{
    private readonly IFolioRepository _repository;

    public FoliosController(IFolioRepository repository)
    {
        _repository = repository;
    }

    // GET api/folios/reservation/{id}
    [HttpGet(""reservation/{reservationId}"")]
    public async Task<ActionResult<FolioDto>> GetByReservation(Guid reservationId)
    {
        var folio = await _repository.GetByReservationIdAsync(reservationId);
        if (folio == null) return NotFound(""No se encontró folio para esta reserva"");

        return Ok(MapToDto(folio));
    }

    // GET api/folios/{id}
    [HttpGet(""{id}"")]
    public async Task<ActionResult<FolioDto>> GetById(Guid id)
    {
        var folio = await _repository.GetByIdAsync(id);
        if (folio == null) return NotFound();

        return Ok(MapToDto(folio));
    }

    // POST api/folios/{id}/transactions
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
            CreatedByUserId = ""Admin"" // En el futuro vendrá del Token JWT
        };

        await _repository.AddTransactionAsync(transaction);

        return Ok(new { message = ""Transacción agregada"", transactionId = transaction.Id });
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
Set-Content -Path "$ControllerPath/FoliosController.cs" -Value $ContentController

Write-Host "¡Módulo de Folios generado!" -ForegroundColor Green