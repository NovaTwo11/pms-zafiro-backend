using PmsZafiro.Domain.Entities;

namespace PmsZafiro.Application.Interfaces;

public interface IGuestRepository
{
    Task<IEnumerable<Guest>> GetAllAsync();
    Task<Guest?> GetByIdAsync(Guid id);
    Task<Guest> AddAsync(Guest guest);
    Task UpdateAsync(Guest guest);
    Task DeleteAsync(Guid id);
}
