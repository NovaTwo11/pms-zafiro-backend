using Microsoft.EntityFrameworkCore;
using PmsZafiro.Application.Interfaces;
using PmsZafiro.Domain.Entities;
using PmsZafiro.Domain.Enums;
using PmsZafiro.Infrastructure.Persistence;

namespace PmsZafiro.Infrastructure.Repositories;

public class RoomRepository : IRoomRepository
{
    private readonly PmsDbContext _context;

    public RoomRepository(PmsDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<Room>> GetAllAsync()
    {
        return await _context.Rooms.OrderBy(r => r.Number).ToListAsync();
    }
    
    public async Task<IEnumerable<Room>> GetByStatusAsync(RoomStatus status)
    {
        return await _context.Rooms
            .Where(r => r.Status == status)
            .OrderBy(r => r.Number)
            .ToListAsync();
    }

    public async Task<Room?> GetByIdAsync(Guid id)
    {
        return await _context.Rooms.FindAsync(id);
    }

    public async Task<Room> AddAsync(Room room)
    {
        _context.Rooms.Add(room);
        await _context.SaveChangesAsync();
        return room;
    }

    public async Task UpdateAsync(Room room)
    {
        _context.Entry(room).State = EntityState.Modified;
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(Guid id)
    {
        var room = await _context.Rooms.FindAsync(id);
        if (room != null)
        {
            _context.Rooms.Remove(room);
            await _context.SaveChangesAsync();
        }
    }
}
