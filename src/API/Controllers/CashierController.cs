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
        var status = await _service.GetStatusAsync("user1");
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
}