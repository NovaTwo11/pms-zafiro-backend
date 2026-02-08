using PmsZafiro.Domain.Entities;
using PmsZafiro.Domain.Enums;

namespace PmsZafiro.Application.Interfaces;

public interface IRoomRepository
{
    Task<IEnumerable<Room>> GetAllAsync();
    Task<IEnumerable<Room>> GetByStatusAsync(RoomStatus status);
    Task<Room?> GetByIdAsync(Guid id);
    Task<Room> AddAsync(Room room);
    Task UpdateAsync(Room room);
    Task DeleteAsync(Guid id);
}
