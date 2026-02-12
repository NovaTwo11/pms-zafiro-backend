using Microsoft.EntityFrameworkCore;
using PmsZafiro.Infrastructure.Persistence;
using PmsZafiro.Application.Interfaces;
using PmsZafiro.Application.Services;
using PmsZafiro.Infrastructure.Repositories;

var builder = WebApplication.CreateBuilder(args);

// 1. Configuración de Base de Datos
builder.Services.AddDbContext<PmsDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// 2. Configuración de CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowNextJs", policy =>
    {
        // NOTA: Cuando subas a producción, deberás agregar aquí tu dominio real (ej. "https://mihotel.com")
        policy.WithOrigins("http://localhost:3000") 
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

// 3. Configuración de Swagger/OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        // Esto asegura que el Backend devuelva JSON en camelCase (estándar JS)
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    });

// 4. Inyección de Dependencias
builder.Services.AddScoped<IReservationRepository, ReservationRepository>();
builder.Services.AddScoped<IGuestRepository, GuestRepository>();
builder.Services.AddScoped<IRoomRepository, RoomRepository>();
builder.Services.AddScoped<IFolioRepository, FolioRepository>();
builder.Services.AddScoped<ICashierRepository, CashierRepository>();
builder.Services.AddScoped<CashierService>();
builder.Services.AddScoped<INotificationRepository, NotificationRepository>();
builder.Services.AddScoped<IProductRepository, ProductRepository>();

// 5. Workers
builder.Services.AddHostedService<PmsZafiro.API.Workers.HousekeepingWorker>();

var app = builder.Build();

// 6. Configuración del Pipeline HTTP
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    // En Desarrollo: NO usamos HttpsRedirection para evitar errores de certificado local
}
else
{
    // En Producción: SÍ forzamos HTTPS por seguridad
    app.UseHttpsRedirection();
}

app.UseCors("AllowNextJs");

app.MapControllers(); 

app.Run();