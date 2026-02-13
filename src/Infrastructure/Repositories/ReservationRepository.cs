using Microsoft.EntityFrameworkCore;
using PmsZafiro.Application.Interfaces;
using PmsZafiro.Domain.Entities;
using PmsZafiro.Domain.Enums;
using PmsZafiro.Infrastructure.Persistence;

namespace PmsZafiro.Infrastructure.Repositories;

public class ReservationRepository : IReservationRepository
{
    private readonly PmsDbContext _context;

    public ReservationRepository(PmsDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<Reservation>> GetAllAsync()
    {
        return await _context.Reservations
            .Include(r => r.Guest)
            .Include(r => r.Room)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();
    }

    public async Task<Reservation?> GetByIdAsync(Guid id)
    {
        return await _context.Reservations
            .Include(r => r.Guest)
            .Include(r => r.Room)
            .FirstOrDefaultAsync(r => r.Id == id);
    }

    public async Task<Reservation> CreateAsync(Reservation reservation)
    {
        await _context.Reservations.AddAsync(reservation);
        await _context.SaveChangesAsync();
        return reservation;
    }

    public async Task UpdateAsync(Reservation reservation)
    {
        _context.Reservations.Update(reservation);
        await _context.SaveChangesAsync();
    }

    public async Task<Reservation?> GetByCodeAsync(string code)
    {
        return await _context.Reservations
            .Include(r => r.Guest)
            .Include(r => r.Room)
            .FirstOrDefaultAsync(r => r.ConfirmationCode == code);
    }

    // --- LÓGICA DE CHECK-OUT TRANSACCIONAL ---
    // Utiliza ExecutionStrategy para evitar errores de conexión intermitentes
    // y maneja transacciones explícitas para asegurar integridad.
    public async Task ProcessCheckOutAsync(Reservation reservation, Room? room, Folio folio)
    {
        var strategy = _context.Database.CreateExecutionStrategy();

        await strategy.ExecuteAsync(async () =>
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // 1. Actualizar Reserva
                _context.Reservations.Update(reservation);
                
                // 2. Actualizar Folio
                _context.Folios.Update(folio);

                // 3. Actualizar Habitación (si existe)
                if (room != null)
                {
                    _context.Rooms.Update(room);
                }

                // 4. Guardar y confirmar
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw; // Re-lanza la excepción para que el Controller la registre
            }
        });
    }

    public async Task<IEnumerable<Reservation>> GetActiveReservationsByRoomAsync(Guid roomId)
    {
        return await _context.Reservations
            .Where(r => r.RoomId == roomId &&
                       (r.Status == ReservationStatus.Confirmed || r.Status == ReservationStatus.CheckedIn))
            .ToListAsync();
    }
    
    public async Task ProcessCheckInAsync(Reservation reservation, Room room, GuestFolio newFolio)
    {
        var strategy = _context.Database.CreateExecutionStrategy();

        await strategy.ExecuteAsync(async () =>
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // 1. Actualizar Reserva
                _context.Reservations.Update(reservation);
            
                // 2. Actualizar Habitación
                _context.Rooms.Update(room);

                // 3. Crear Folio (Solo si no existe, validado en Controller, pero aquí se persiste)
                await _context.Folios.AddAsync(newFolio);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        });
    }
}