using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using PmsZafiro.Domain.Entities;
using PmsZafiro.Infrastructure.Persistence;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.EntityFrameworkCore;

namespace PmsZafiro.API.Controllers;

[Route("api/[controller]")]
[ApiController]
public class AuthController : ControllerBase
{
    private readonly PmsDbContext _context;
    private readonly IConfiguration _configuration;

    public AuthController(PmsDbContext context, IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginDto request)
    {
        // 1. Validar usuario
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Username == request.Username);

        // Validación simple (en producción usar hashing real)
        if (user == null || user.PasswordHash != request.Password) 
        {
            return Unauthorized("Credenciales inválidas.");
        }

        // 2. Crear Claims
        var claims = new List<System.Security.Claims.Claim>
        {
            new System.Security.Claims.Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new System.Security.Claims.Claim(ClaimTypes.Name, user.Username),
            new System.Security.Claims.Claim(ClaimTypes.Role, user.Role)
        };

        // 3. Generar la llave
        var keyString = _configuration.GetSection("JwtSettings:Key").Value;
        if (string.IsNullOrEmpty(keyString)) return StatusCode(500, "JWT Key no configurada");

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(keyString));
        
        // CORRECCIÓN AQUÍ: Usamos HmacSha256 en lugar de HmacSha512Signature
        // Esto funciona perfectamente con tu clave actual de 424 bits.
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        // 4. Crear el token
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddMinutes(60),
            SigningCredentials = creds,
            Issuer = "PmsZafiroAPI", 
            Audience = "PmsZafiroClient"
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        var token = tokenHandler.CreateToken(tokenDescriptor);
        var jwt = tokenHandler.WriteToken(token);

        return Ok(new { token = jwt, role = user.Role });
    }
}

public class LoginDto
{
    public required string Username { get; set; }
    public required string Password { get; set; }
}