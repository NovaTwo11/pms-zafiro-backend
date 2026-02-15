using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using PmsZafiro.Infrastructure.Persistence;
using PmsZafiro.Domain.Entities;
using PmsZafiro.Domain.Enums;
using System.IO;
using System.Threading.Tasks;

namespace PmsZafiro.API.Controllers;

[ApiController]
[Route("api/webhooks/booking")]
public class BookingWebhookController : ControllerBase
{
    private readonly PmsDbContext _context;
    private readonly ILogger<BookingWebhookController> _logger;

    public BookingWebhookController(PmsDbContext context, ILogger<BookingWebhookController> logger)
    {
        _context = context;
        _logger = logger;
    }

    [HttpPost("reservation-push")]
    public async Task<IActionResult> ReceiveReservationPush()
    {
        // 1. Leer el body crudo (XML o JSON) que nos manda Booking.com
        using var reader = new StreamReader(Request.Body);
        var rawPayload = await reader.ReadToEndAsync();

        _logger.LogInformation("--- WEBHOOK DE BOOKING RECIBIDO ---");
        
        // 2. Guardar en el Inbox para procesarlo después (Patrón Inbox)
        var inboundEvent = new IntegrationInboundEvent
        {
            Channel = BookingChannel.BookingCom,
            Payload = rawPayload,
            IsProcessed = false
        };

        _context.IntegrationInboundEvents.Add(inboundEvent);
        await _context.SaveChangesAsync();

        // 3. Responder rápido a Booking.com para que no asuma timeout (Overbooking prevention)
        return Ok(new { status = "success", message = "Payload received and queued for processing" });
    }
}