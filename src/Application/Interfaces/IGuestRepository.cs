using PmsZafiro.Domain.Entities;

namespace PmsZafiro.Application.Interfaces;

public interface IGuestRepository
{
    Task<IEnumerable<Guest>> GetAllAsync();
    Task<IEnumerable<Guest>> GetAllWithHistoryAsync(); 
    Task<Guest?> GetByIdAsync(Guid id);
    Task<Guest?> GetByDocumentAsync(string documentNumber);
    Task AddAsync(Guest guest);
    Task UpdateAsync(Guest guest);
    Task DeleteAsync(Guid id);
}
