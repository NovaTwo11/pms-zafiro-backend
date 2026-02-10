using Microsoft.EntityFrameworkCore;
using PmsZafiro.Infrastructure.Persistence;
using PmsZafiro.Application.Interfaces;
using PmsZafiro.Application.Services;
using PmsZafiro.Infrastructure.Repositories;

var builder = WebApplication.CreateBuilder(args);

// 1. Configuración de Base de Datos
builder.Services.AddDbContext<PmsDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// 2. Configuración de CORS (Para que el Frontend Next.js pueda conectarse)
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowNextJs", policy =>
    {
        policy.WithOrigins("http://localhost:3000") // URL del Frontend
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

// 3. Configuración de Swagger/OpenAPI (Reemplazo de .NET Native OpenAPI)
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(); // Generador de Swagger

builder.Services.AddControllers();

// 4. Inyección de Dependencias (Repositorios y Servicios)
builder.Services.AddScoped<IReservationRepository, ReservationRepository>();
builder.Services.AddScoped<IGuestRepository, GuestRepository>();
builder.Services.AddScoped<IRoomRepository, RoomRepository>();
builder.Services.AddScoped<IFolioRepository, FolioRepository>();
builder.Services.AddScoped<ICashierRepository, CashierRepository>();
builder.Services.AddScoped<CashierService>();
builder.Services.AddScoped<INotificationRepository, NotificationRepository>();
builder.Services.AddScoped<IProductRepository, ProductRepository>();

// 5. Workers (Tareas en segundo plano)
builder.Services.AddHostedService<PmsZafiro.API.Workers.HousekeepingWorker>();

var app = builder.Build();

// 6. Configuración del Pipeline HTTP
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();   // Genera el JSON de Swagger
    app.UseSwaggerUI(); // Genera la Interfaz Gráfica en /swagger
}

app.UseHttpsRedirection();
app.UseCors("AllowNextJs");

app.MapControllers(); 

app.Run();