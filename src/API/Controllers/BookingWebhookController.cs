// src/API/Controllers/BookingWebhookController.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.IO;
using System.Threading.Tasks;

namespace PmsZafiro.API.Controllers;

[ApiController]
[Route("api/webhooks/booking")]
public class BookingWebhookController : ControllerBase
{
    private readonly ILogger<BookingWebhookController> _logger;
    // Aquí luego inyectaremos el repositorio para guardar en el Inbox

    public BookingWebhookController(ILogger<BookingWebhookController> logger)
    {
        _logger = logger;
    }

    [HttpPost("reservation-push")]
    public async Task<IActionResult> ReceiveReservationPush()
    {
        // 1. Leer el body crudo (Booking puede enviar XML o JSON dependiendo de la configuración de tu cuenta)
        using var reader = new StreamReader(Request.Body);
        var rawPayload = await reader.ReadToEndAsync();

        // 2. Registrar el payload para auditoría y debug (¡Clave para ver qué nos mandan!)
        _logger.LogInformation("--- WEBHOOK DE BOOKING RECIBIDO ---");
        _logger.LogInformation("Payload: {Payload}", rawPayload);

        // 3. TO-DO (Próximo paso): Guardar 'rawPayload' en una tabla 'IntegrationInboundEvents' (Inbox Pattern)

        // 4. Responder INMEDIATAMENTE con un 200 OK para que Booking sepa que lo recibimos
        return Ok(new { status = "success", message = "Payload received and queued for processing" });
    }
}