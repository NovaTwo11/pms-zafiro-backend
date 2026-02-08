using Microsoft.EntityFrameworkCore;
using PmsZafiro.Infrastructure.Persistence;
using PmsZafiro.Application.Interfaces;
using PmsZafiro.Infrastructure.Repositories;

var builder = WebApplication.CreateBuilder(args);

// 1. Configuración de Base de Datos
builder.Services.AddDbContext<PmsDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// 2. Servicios de la API
builder.Services.AddOpenApi();
builder.Services.AddControllers(); // Registra los controladores en la memoria

// 3. Inyección de Dependencias (Tus Repositorios)
builder.Services.AddScoped<IReservationRepository, ReservationRepository>();
builder.Services.AddScoped<IGuestRepository, GuestRepository>();
builder.Services.AddScoped<IRoomRepository, RoomRepository>();
builder.Services.AddScoped<IFolioRepository, FolioRepository>();
builder.Services.AddScoped<INotificationRepository, NotificationRepository>();


var app = builder.Build();

// 4. Configuración del Pipeline HTTP
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi(); // Genera el JSON de la API
}

app.UseHttpsRedirection();

// 5. ¡LA LÍNEA QUE FALTABA!
// Esto le dice a la app: "Busca todos los [Route] en los controladores y actívalos"
app.MapControllers(); 

// (El endpoint de clima de ejemplo lo puedes dejar o borrar, no afecta)
var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/weatherforecast", () =>
    {
        var forecast =  Enumerable.Range(1, 5).Select(index =>
                new WeatherForecast
                (
                    DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
                    Random.Shared.Next(-20, 55),
                    summaries[Random.Shared.Next(summaries.Length)]
                ))
            .ToArray();
        return forecast;
    })
    .WithName("GetWeatherForecast");

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}