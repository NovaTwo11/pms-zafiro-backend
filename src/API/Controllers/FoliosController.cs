using Microsoft.AspNetCore.Mvc;
using PmsZafiro.Application.DTOs.Folios;
using PmsZafiro.Application.Interfaces;
using PmsZafiro.Application.Services;
using PmsZafiro.Domain.Entities;
using PmsZafiro.Domain.Enums;

namespace PmsZafiro.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class FoliosController : ControllerBase
{
    private readonly IFolioRepository _repository;
    private readonly CashierService _cashierService;
    private readonly IProductRepository _productRepository;
    private readonly IEmailService _emailService;
    private readonly IConfiguration _config;

    public FoliosController(IFolioRepository repository, CashierService cashierService, IProductRepository productRepository, IEmailService emailService, IConfiguration config)
    {
        _repository = repository;
        _cashierService = cashierService;
        _productRepository = productRepository;
        _emailService = emailService;
        _config = config;
    }
    
    [HttpPost("{id}/send-invoice")]
    public async Task<IActionResult> SendInvoice(Guid id)
    {
        var folio = await _repository.GetByIdAsync(id); 
        if (folio == null) return NotFound("Folio no encontrado");

        string toEmail = "";
        string guestName = "";
        string roomNumber = "No asignada";
        string dates = "N/A";
        string confirmationCode = "N/A";

        if (folio is GuestFolio gf && gf.Reservation != null)
        {
            if (gf.Reservation.Guest != null)
            {
                toEmail = gf.Reservation.Guest.Email;
                guestName = gf.Reservation.Guest.FullName;
            }
            
            confirmationCode = gf.Reservation.ConfirmationCode ?? gf.Reservation.Id.ToString()[..8].ToUpper();
            dates = $"{gf.Reservation.CheckIn:dd/MM/yyyy} - {gf.Reservation.CheckOut:dd/MM/yyyy}";

            // Intentar sacar la habitación si los segmentos están cargados
            var activeSegment = gf.Reservation.Segments?.OrderBy(s => s.CheckIn).FirstOrDefault();
            if (activeSegment?.Room != null) 
            {
                roomNumber = activeSegment.Room.Number;
            }
        }
        else if (folio is ExternalFolio ef) 
        {
            toEmail = "cliente@externo.com"; // Opcional: Si en un futuro agregas email a clientes externos
            guestName = ef.Alias ?? "Cliente Externo";
            confirmationCode = "EXT-" + ef.Id.ToString()[..6].ToUpper();
        }

        if (string.IsNullOrEmpty(toEmail))
            return BadRequest("No hay un correo registrado para esta cuenta.");

        var charges = folio.Transactions.Where(t => t.Type == TransactionType.Charge).Sum(t => t.Amount);
        var payments = folio.Transactions.Where(t => t.Type == TransactionType.Payment).Sum(t => t.Amount);
        var balance = charges - payments;

        // Construir dinámicamente las filas de la tabla HTML con cada transacción
        var transactionsHtml = string.Join("", folio.Transactions.OrderBy(t => t.CreatedAt).Select(t => $@"
            <tr>
                <td style='padding: 12px 10px; border-bottom: 1px solid #eee; font-size: 13px; color: #555;'>{t.CreatedAt.ToLocalTime():dd/MM/yyyy HH:mm}</td>
                <td style='padding: 12px 10px; border-bottom: 1px solid #eee; font-size: 13px; color: #333;'>{t.Description}</td>
                <td style='padding: 12px 10px; border-bottom: 1px solid #eee; font-size: 13px; color: #555; text-align: center;'>{t.Quantity}</td>
                <td style='padding: 12px 10px; border-bottom: 1px solid #eee; font-size: 14px; font-weight: 500; color: {(t.Type == TransactionType.Charge ? "#555" : "#059669")}; text-align: right;'>
                    {(t.Type == TransactionType.Charge ? "" : "- ")} {t.Amount:C0}
                </td>
            </tr>
        "));

        // Si no hay transacciones, mostrar un mensaje bonito
        if (string.IsNullOrEmpty(transactionsHtml))
        {
            transactionsHtml = "<tr><td colspan='4' style='padding: 20px; text-align: center; color: #999; font-style: italic;'>No hay movimientos registrados en esta cuenta.</td></tr>";
        }

        // Color del saldo: Rojo si debe, Verde si está a paz y salvo
        string balanceColor = balance > 0 ? "#cf6679" : (balance == 0 ? "#059669" : "#333");
        string balanceText = balance > 0 ? "Saldo Pendiente" : (balance == 0 ? "Cuenta Saldada" : "Saldo a Favor");

        var body = $@"
        <div style='font-family: ""Segoe UI"", Tahoma, Geneva, Verdana, sans-serif; max-width: 700px; margin: 0 auto; border-radius: 8px; overflow: hidden; box-shadow: 0 4px 15px rgba(0,0,0,0.1); border: 1px solid #e0e0e0; background-color: #ffffff;'>
            
            <div style='background-color: #111111; padding: 30px; text-align: center; border-bottom: 4px solid #D4AF37;'>
                <h1 style='color: #D4AF37; margin: 0; font-size: 26px; letter-spacing: 2px; text-transform: uppercase;'>HOTEL ZAFIRO</h1>
                <p style='color: #ffffff; margin: 5px 0 0 0; font-size: 14px; letter-spacing: 1px;'>ESTADO DE CUENTA / FACTURA PRELIMINAR</p>
            </div>

            <div style='padding: 30px; background-color: #fdfbf5; border-bottom: 1px solid #eeeeee;'>
                <table width='100%' style='border-collapse: collapse;'>
                    <tr>
                        <td width='50%' style='vertical-align: top;'>
                            <p style='margin: 0 0 5px 0; font-size: 13px; color: #777; text-transform: uppercase;'>Facturado a:</p>
                            <p style='margin: 0; font-size: 18px; font-weight: bold; color: #333;'>{guestName}</p>
                            <p style='margin: 5px 0 0 0; font-size: 14px; color: #555;'>{toEmail}</p>
                        </td>
                        <td width='50%' style='vertical-align: top; text-align: right;'>
                            <p style='margin: 0 0 5px 0; font-size: 13px; color: #777; text-transform: uppercase;'>Detalles de la Cuenta:</p>
                            <p style='margin: 0; font-size: 15px; color: #333;'><strong>Reserva:</strong> #{confirmationCode}</p>
                            <p style='margin: 3px 0 0 0; font-size: 15px; color: #333;'><strong>Habitación:</strong> {roomNumber}</p>
                            <p style='margin: 3px 0 0 0; font-size: 14px; color: #555;'><strong>Fechas:</strong> {dates}</p>
                        </td>
                    </tr>
                </table>
            </div>

            <div style='padding: 30px;'>
                <h3 style='color: #D4AF37; margin-top: 0; margin-bottom: 15px; font-size: 18px; border-bottom: 1px solid #eee; padding-bottom: 10px;'>Detalle de Movimientos</h3>
                <table width='100%' style='border-collapse: collapse; margin-bottom: 20px;'>
                    <thead>
                        <tr style='background-color: #f5f5f5;'>
                            <th style='padding: 12px 10px; text-align: left; font-size: 13px; color: #333; border-bottom: 2px solid #ddd; text-transform: uppercase;'>Fecha</th>
                            <th style='padding: 12px 10px; text-align: left; font-size: 13px; color: #333; border-bottom: 2px solid #ddd; text-transform: uppercase;'>Concepto</th>
                            <th style='padding: 12px 10px; text-align: center; font-size: 13px; color: #333; border-bottom: 2px solid #ddd; text-transform: uppercase;'>Cant.</th>
                            <th style='padding: 12px 10px; text-align: right; font-size: 13px; color: #333; border-bottom: 2px solid #ddd; text-transform: uppercase;'>Total</th>
                        </tr>
                    </thead>
                    <tbody>
                        {transactionsHtml}
                    </tbody>
                </table>

                <div style='width: 100%; display: flex; justify-content: flex-end;'>
                    <table style='border-collapse: collapse; width: 320px; float: right;'>
                        <tr>
                            <td style='padding: 8px; font-size: 14px; color: #555;'>Total Cargos / Consumos:</td>
                            <td style='padding: 8px; font-size: 15px; color: #333; text-align: right; font-weight: bold;'>{charges:C0}</td>
                        </tr>
                        <tr>
                            <td style='padding: 8px; font-size: 14px; color: #555;'>Total Pagos y Abonos:</td>
                            <td style='padding: 8px; font-size: 15px; color: #059669; text-align: right; font-weight: bold;'>- {payments:C0}</td>
                        </tr>
                        <tr>
                            <td colspan='2' style='padding: 0;'>
                                <div style='height: 2px; background-color: #e0e0e0; margin: 5px 0;'></div>
                            </td>
                        </tr>
                        <tr>
                            <td style='padding: 15px 8px; font-size: 16px; color: #111; font-weight: bold; text-transform: uppercase;'>{balanceText}:</td>
                            <td style='padding: 15px 8px; font-size: 20px; color: {balanceColor}; text-align: right; font-weight: bold;'>{balance:C0}</td>
                        </tr>
                    </table>
                    <div style='clear: both;'></div>
                </div>
            </div>

            <div style='background-color: #f9f9f9; padding: 25px; text-align: center; border-top: 1px solid #eeeeee;'>
                <p style='color: #333333; font-weight: bold; margin: 0 0 5px 0; font-size: 14px;'>HOTEL ZAFIRO DORADAL</p>
                <p style='color: #777777; margin: 0 0 15px 0; font-size: 13px;'>+57 3202095352 • zafirohoteldoradal@gmail.com</p>
                <p style='color: #aaaaaa; margin: 0; font-size: 11px; line-height: 1.4;'>Este documento es un resumen de su estado de cuenta a la fecha.<br>Conserve este correo para futuras referencias.</p>
            </div>
        </div>";

        await _emailService.SendEmailAsync(toEmail, $"Estado de Cuenta - Reserva #{confirmationCode}", body);
        return Ok(new { message = "Factura enviada exitosamente." });
    }
    
    [HttpGet("active-guests")]
    public async Task<ActionResult<IEnumerable<object>>> GetActiveGuests()
    {
        // 1. Traer datos
        var folios = await _repository.GetActiveGuestFoliosAsync();
        var result = new List<object>();

        // 2. Iteración SECUENCIAL (Fix para evitar error de concurrencia de DbContext)
        foreach (var f in folios)
        {
            // Consulta segura al repo (una por una)
            var dbBalance = await _repository.GetFolioBalanceAsync(f.Id);

            var nights = 0;
            string roomNumber = "?";
            DateTime? checkIn = null;
            DateTime? checkOut = null;
            string guestName = "Desconocido";

            if (f.Reservation != null)
            {
                guestName = f.Reservation.Guest?.FullName ?? "Desconocido";
                checkIn = f.Reservation.CheckIn;
                checkOut = f.Reservation.CheckOut;
            
                nights = (f.Reservation.CheckOut - f.Reservation.CheckIn).Days;
                if (nights < 1) nights = 1;

                // Null safety para segmentos
                var segments = f.Reservation.Segments;
                if (segments != null && segments.Any())
                {
                    var activeSegment = segments
                                            .OrderBy(s => s.CheckIn)
                                            .FirstOrDefault(s => s.CheckIn <= DateTime.UtcNow && s.CheckOut > DateTime.UtcNow) 
                                        ?? segments.FirstOrDefault();

                    roomNumber = activeSegment?.Room?.Number ?? "?";
                }
            }

            result.Add(new 
            {
                Id = f.Id,
                Type = "guest",
                Status = f.Status.ToString(),
                Balance = dbBalance, 
                GuestName = guestName,
                RoomNumber = roomNumber,
                CheckIn = checkIn,
                CheckOut = checkOut,
                Nights = nights
            });
        }
    
        return Ok(result);
    }

    [HttpGet("active-externals")]
    public async Task<ActionResult<IEnumerable<object>>> GetActiveExternals()
    {
        var allFolios = await _repository.GetAllAsync(); 
        var externals = allFolios.OfType<ExternalFolio>().Where(f => f.Status == FolioStatus.Open).ToList();
        var result = new List<object>();

        // Fix Concurrencia aquí también
        foreach (var f in externals)
        {
            var dbBalance = await _repository.GetFolioBalanceAsync(f.Id);
            result.Add(new 
            {
                Id = f.Id,
                Type = "external",
                Status = f.Status.ToString(),
                Balance = dbBalance,
                Alias = f.Alias ?? "Cliente Externo",
                Description = f.Description,
                CreatedAt = f.CreatedAt
            });
        }
        
        return Ok(result);
    }
    
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteExternalFolio(Guid id)
    {
        var folio = await _repository.GetByIdAsync(id);
        if (folio == null) return NotFound("Folio no encontrado.");

        // 1er Candado: Solo permitir borrar folios externos
        if (folio is not ExternalFolio)
            return BadRequest(new { message = "Operación denegada. Solo se pueden eliminar folios de pasadías/externos." });

        // 2do Candado: No borrar si hay dinero de por medio sin cuadrar
        var balance = await _repository.GetFolioBalanceAsync(id);
        if (balance != 0)
            return BadRequest(new { message = "No se puede eliminar un folio con saldo a favor o en contra." });

        // Llama al método de borrado de tu repositorio
        // (Nota: Asegúrate de tener DeleteAsync o RemoveAsync definido en tu IFolioRepository)
        await _repository.DeleteAsync(folio); 
        
        return Ok(new { message = "Folio externo eliminado correctamente." });
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<FolioDto>> GetById(Guid id)
    {
        var folio = await _repository.GetByIdAsync(id);
        if (folio == null) return NotFound();
        return Ok(MapToDto(folio));
    }

    [HttpPost("external")]
    public async Task<IActionResult> CreateExternalFolio([FromBody] CreateExternalFolioDto dto)
    {
        var folio = new ExternalFolio
        {
            Alias = dto.Alias,
            Description = dto.Description,
            Status = FolioStatus.Open,
            CreatedAt = DateTimeOffset.UtcNow
        };
        await _repository.AddAsync(folio);
        return CreatedAtAction(nameof(GetById), new { id = folio.Id }, new { id = folio.Id, message = "Folio externo creado" });
    }
    
    [HttpPost("{id}/transactions")]
    public async Task<IActionResult> AddTransaction(Guid id, [FromBody] CreateTransactionDto dto)
    {
        // 1. Obtener usuario actual
        string currentUserId = "user1"; 

        // 2. Buscar turno abierto
        var openShift = await _cashierService.GetOpenShiftEntityAsync(currentUserId);

        if (openShift == null)
        {
            return BadRequest(new { 
                error = "Caja Cerrada", 
                message = "No hay un turno de caja abierto. Por favor abra la caja antes de registrar movimientos." 
            });
        }

        // ========================================================================
        // 🔴 FIX CRÍTICO: RED DE SEGURIDAD PARA EL TIPO DE TRANSACCIÓN
        // ========================================================================
        // Si el JSON Binding falló y 'Type' llegó como 0 (Charge) por defecto,
        // pero la descripción dice explícitamente "Pago", lo corregimos manualmente.
        if (dto.Type == TransactionType.Charge && 
           (dto.Description.Contains("Pago", StringComparison.OrdinalIgnoreCase) || 
            dto.Description.Contains("Abono", StringComparison.OrdinalIgnoreCase)))
        {
            dto.Type = TransactionType.Payment; // Forzamos el tipo correcto (1)
        }
        // ========================================================================

        // VALIDACIÓN ANTI-ERROR CONTABLE
        // Solo los Cargos pueden ser negativos (correcciones). Los Pagos siempre entran positivos y el sistema los resta.
        if (dto.Amount < 0 && dto.Type != TransactionType.Charge)
        {
             return BadRequest(new { 
                error = "Operación Inválida", 
                message = "Solo los Cargos pueden tener valor negativo. Los Pagos deben ser positivos." 
            });
        }

        var folio = await _repository.GetByIdAsync(id);
        if (folio == null) return NotFound("El folio no existe.");
        
        // Validación de método de pago
        if (dto.Type == TransactionType.Payment && (int)dto.PaymentMethod < 1)
        {
            // Si es pago pero viene sin método, asignamos Efectivo por defecto para evitar error
            if (dto.PaymentMethod == PaymentMethod.None) 
                dto.PaymentMethod = PaymentMethod.Cash;
            else
                return BadRequest("Para registrar un pago debe especificar un método de pago válido.");
        }

        // 3. Crear la transacción
        var transaction = new FolioTransaction
        {
            Id = Guid.NewGuid(),
            FolioId = id,
            Amount = dto.Amount,
            Description = dto.Description,
            Type = dto.Type, // Aquí ya lleva el valor corregido (1 si es pago)
            Quantity = dto.Quantity > 0 ? dto.Quantity : 1,
            UnitPrice = dto.UnitPrice > 0 ? dto.UnitPrice : (dto.Amount < 0 ? -dto.Amount : dto.Amount),
            // Si es Cargo, PaymentMethod es None. Si es Pago, usamos el del DTO.
            PaymentMethod = dto.Type == TransactionType.Charge ? PaymentMethod.None : dto.PaymentMethod,        
            CashierShiftId = openShift.Id,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedByUserId = currentUserId
        };

        await _repository.AddTransactionAsync(transaction);
        
        // Descuento de inventario (solo si aplica)
        if (dto.ProductId.HasValue && dto.Type == TransactionType.Charge)
        {
            var product = await _productRepository.GetByIdAsync(dto.ProductId.Value);
            if (product != null && product.IsStockTracked)
            {
                product.Stock -= dto.Quantity;
                if (product.Stock < 0) product.Stock = 0; 

                await _productRepository.UpdateAsync(product);
            }
        }

        return Ok(new { message = "Transacción registrada correctamente", transactionId = transaction.Id });
    }

    private static FolioDto MapToDto(Folio folio)
    {
        var charges = folio.Transactions.Where(t => t.Type == TransactionType.Charge).Sum(t => t.Amount);
        var payments = folio.Transactions.Where(t => t.Type == TransactionType.Payment).Sum(t => t.Amount);
        
        var dto = new FolioDto
        {
            Id = folio.Id,
            CreatedAt = folio.CreatedAt.DateTime, 
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
                PaymentMethod = t.PaymentMethod 
            }).ToList()
        };

        if (folio is GuestFolio guestFolio && guestFolio.Reservation != null)
        {
            dto.FolioType = "guest";
            dto.ReservationId = guestFolio.ReservationId;
            
            var segments = guestFolio.Reservation.Segments;
            var activeSegment = (segments != null && segments.Any())
                ? segments.OrderBy(s => s.CheckIn)
                          .FirstOrDefault(s => s.CheckIn <= DateTime.UtcNow && s.CheckOut > DateTime.UtcNow) 
                  ?? segments.FirstOrDefault()
                : null;

            dto.GuestName = guestFolio.Reservation.Guest?.FullName ?? "Huésped";
            dto.RoomNumber = activeSegment?.Room?.Number ?? "N/A"; 
            dto.CheckIn = guestFolio.Reservation.CheckIn;
            dto.CheckOut = guestFolio.Reservation.CheckOut;
            
            var nights = (guestFolio.Reservation.CheckOut - guestFolio.Reservation.CheckIn).Days;
            dto.Nights = nights < 1 ? 1 : nights;
        }
        else if (folio is ExternalFolio externalFolio)
        {
            dto.FolioType = "external";
            dto.Alias = externalFolio.Alias;
            dto.Description = externalFolio.Description;
        }
        
        return dto;
    }
}