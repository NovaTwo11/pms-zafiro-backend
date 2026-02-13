using Microsoft.AspNetCore.Mvc;
using PmsZafiro.Application.DTOs.Cashier;
using PmsZafiro.Application.Services;

namespace PmsZafiro.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CashierController : ControllerBase
{
    private readonly CashierService _service;
    public CashierController(CashierService service) { _service = service; }

    [HttpGet("status")]
    public async Task<ActionResult<CashierShiftDto>> GetStatus()
    {
        var status = await _service.GetStatusAsync("user1"); // Hardcoded temporalmente
        if (status == null) return NoContent();
        return Ok(status);
    }

    [HttpPost("open")]
    public async Task<ActionResult<CashierShiftDto>> OpenShift([FromBody] OpenShiftDto dto)
    {
        try { return Ok(await _service.OpenShiftAsync("user1", dto.StartingAmount)); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
    }

    [HttpPost("close")]
    public async Task<ActionResult<CashierShiftDto>> CloseShift([FromBody] CloseShiftDto dto)
    {
        try { return Ok(await _service.CloseShiftAsync("user1", dto.ActualAmount)); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
    }
    
    [HttpGet("report")]
    public async Task<ActionResult<CashierReportDto>> GetCurrentReport()
    {
        var report = await _service.GetCurrentShiftReportAsync("user1");
        if (report == null) return NotFound("No hay turno abierto para generar reporte.");
        return Ok(report);
    }
    
    [HttpGet("history")]
    public async Task<ActionResult<IEnumerable<CashierShiftDto>>> GetHistory()
    {
        var history = await _service.GetHistoryAsync();
        return Ok(history);
    }

    // --- NUEVO ENDPOINT: Ventas Directas ---
    [HttpPost("direct-sale")]
    public async Task<ActionResult> RegisterDirectSale([FromBody] CreateDirectSaleDto dto)
    {
        try 
        {
            if (dto.TotalAmount < 0) 
                return BadRequest(new { error = "El monto no puede ser negativo." });
            
            if ((int)dto.PaymentMethod < 1) 
                return BadRequest(new { error = "Debe especificar un método de pago válido para la venta directa." });

            await _service.RegisterDirectSaleAsync("user1", dto); // "user1" hardcoded temporalmente al igual que el resto
            
            return Ok(new { message = "Venta directa registrada exitosamente" });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR CRÍTICO] RegisterDirectSale: {ex.Message}");
            return StatusCode(500, new { error = "Error interno del servidor", details = ex.Message });
        }
    }
    
    [HttpPost("movement")]
    public async Task<ActionResult> RegisterMovement([FromBody] CreateCashierMovementDto dto)
    {
        try 
        { 
            if (dto.Amount <= 0) 
                return BadRequest(new { error = "El monto debe ser mayor a 0." });

            await _service.RegisterMovementAsync("user1", dto);
        
            return Ok(new { message = "Movimiento registrado exitosamente" }); 
        }
        catch (ArgumentException ex) // Para errores de "tipo de movimiento"
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex) // Para "Caja cerrada"
        { 
            return BadRequest(new { error = ex.Message }); 
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR CRÍTICO] RegisterMovement: {ex.Message}");
            return StatusCode(500, new { error = "Error interno del servidor", details = ex.Message });
        }
    }
    
}