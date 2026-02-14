using Microsoft.EntityFrameworkCore;
using PmsZafiro.Infrastructure.Persistence;
using PmsZafiro.Application.Interfaces;
using PmsZafiro.Application.Services;
using PmsZafiro.Infrastructure.Repositories;
using Microsoft.AspNetCore.Authentication.JwtBearer; // Necesario
using Microsoft.IdentityModel.Tokens; // Necesario
using System.Text;
using PmsZafiro.Infrastructure.Services; // Necesario

var builder = WebApplication.CreateBuilder(args);

// 1. Configuración de Base de Datos
builder.Services.AddDbContext<PmsDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// 2. Configuración de Seguridad JWT (Autenticación)
// Asegúrate de tener "JwtSettings:Key" en tu appsettings.json
var jwtKey = builder.Configuration["JwtSettings:Key"] ?? "Clave_Secreta_Super_Segura_Por_Defecto_Para_Dev_12345";

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
        ValidateIssuer = false, // Puedes activarlo y configurar "ValidIssuer" en appsettings
        ValidateAudience = false, // Puedes activarlo y configurar "ValidAudience" en appsettings
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero
    };
});

// 3. Configuración de CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowNextJs", policy =>
    {
        policy.WithOrigins("http://localhost:3000") 
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

// 4. Configuración de Swagger/OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    });

// 5. Inyección de Dependencias
builder.Services.AddScoped<IReservationRepository, ReservationRepository>();
builder.Services.AddScoped<IGuestRepository, GuestRepository>();
builder.Services.AddScoped<IRoomRepository, RoomRepository>();
builder.Services.AddScoped<IFolioRepository, FolioRepository>();
builder.Services.AddScoped<ICashierRepository, CashierRepository>();
builder.Services.AddScoped<CashierService>();
builder.Services.AddScoped<INotificationRepository, NotificationRepository>();
builder.Services.AddScoped<IProductRepository, ProductRepository>();
builder.Services.AddScoped<IEmailService, SmtpEmailService>();

// 6. Workers
builder.Services.AddHostedService<PmsZafiro.API.Workers.HousekeepingWorker>();

var app = builder.Build();

// 7. Configuración del Pipeline HTTP
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    app.UseHttpsRedirection();
}

app.UseCors("AllowNextJs");

// IMPORTANTE: El orden importa aquí. Auth -> Authorization
app.UseAuthentication(); 
app.UseAuthorization();

app.MapControllers(); 

app.Run();