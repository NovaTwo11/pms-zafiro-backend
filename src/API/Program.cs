using Microsoft.EntityFrameworkCore;
using PmsZafiro.Infrastructure.Persistence;
using PmsZafiro.Application.Interfaces;
using PmsZafiro.Application.Services;
using PmsZafiro.Infrastructure.Repositories;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using PmsZafiro.Infrastructure.Services;
using PmsZafiro.API.Workers; // Asegúrate de que el namespace coincida con tu HousekeepingWorker

AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var builder = WebApplication.CreateBuilder(args);

// --- 1. CONFIGURACIÓN DE BASE DE DATOS Y VARABLES ---
// Se toma la conexión de las variables de entorno (inyectadas por Docker) o appsettings.json
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<PmsDbContext>(options =>
    options.UseNpgsql(connectionString));

var jwtKey = builder.Configuration["JwtSettings:Key"] ?? throw new Exception("La clave JWT no está configurada.");

// --- 2. CONFIGURACIÓN DE SEGURIDAD (CORS) ---
// Define quién puede consumir esta API
builder.Services.AddCors(options =>
{
    options.AddPolicy("ProductionCorsPolicy", policy =>
    {
        policy.WithOrigins(
                "http://localhost:3000",                  
                "https://pms-zafiro.vercel.app.app",
                "https://hotelzafiro.online"    ,  
                "https://www.hotelzafiro.online"
            )
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials(); // Necesario si usas cookies o headers de autorización
    });
});

// --- 3. AUTENTICACIÓN (JWT) ---
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
        ValidateIssuer = false, 
        ValidateAudience = false,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero
    };
});

// --- 4. DOCUMENTACIÓN Y CONTROLADORES ---
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    });
// --- 4.5 CLIENTE HTTP (NUEVO) ---
builder.Services.AddHttpClient();

// --- 5. INYECCIÓN DE DEPENDENCIAS (CAPA DE INFRAESTRUCTURA & APLICACIÓN) ---
builder.Services.AddScoped<IReservationRepository, ReservationRepository>();
builder.Services.AddScoped<IGuestRepository, GuestRepository>();
builder.Services.AddScoped<IRoomRepository, RoomRepository>();
builder.Services.AddScoped<IFolioRepository, FolioRepository>();
builder.Services.AddScoped<ICashierRepository, CashierRepository>();
builder.Services.AddScoped<INotificationRepository, NotificationRepository>();
builder.Services.AddScoped<IProductRepository, ProductRepository>();

// Servicios
builder.Services.AddScoped<CashierService>();
builder.Services.AddScoped<IEmailService, SmtpEmailService>();

// Workers (Tareas en segundo plano)
builder.Services.AddHostedService<HousekeepingWorker>();
builder.Services.AddHostedService<BookingIntegrationWorker>();
builder.Services.AddHostedService<BookingSyncWorker>();

var app = builder.Build();

// --- 6. MIGRACIONES AUTOMÁTICAS (DevOps Strategy) ---
// Al iniciar el contenedor, esto revisa si la BD existe. Si no, la crea y aplica las tablas.
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<PmsDbContext>();
        if (context.Database.GetPendingMigrations().Any())
        {
            context.Database.Migrate(); // Ejecuta 'dotnet ef database update' internamente
            Console.WriteLine("--> Migraciones aplicadas correctamente.");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"--> Error ejecutando migraciones: {ex.Message}");
    }
}

// --- 7. PIPELINE DE LA APLICACIÓN ---

// Swagge habilitado también en producción (útil para depurar al inicio, puedes quitarlo luego)
app.UseSwagger();
app.UseSwaggerUI();

app.UseHttpsRedirection();

// Aplicar política CORS antes de la autenticación
app.UseCors("ProductionCorsPolicy");

app.UseAuthentication(); 
app.UseAuthorization();

app.MapControllers(); 

app.Run();