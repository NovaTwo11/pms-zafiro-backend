using PmsZafiro.Domain.Entities;

namespace PmsZafiro.Application.Interfaces;

public interface IReservationRepository
{
    Task<IEnumerable<Reservation>> GetAllAsync();
    Task<Reservation?> GetByIdAsync(Guid id);
    Task<Reservation> CreateAsync(Reservation reservation);
    Task UpdateAsync(Reservation reservation);
    Task<Reservation?> GetByCodeAsync(string code);
    
    // NUEVO: Método transaccional para el Check-Out
    Task ProcessCheckOutAsync(Reservation reservation, Room room, Folio folio);
}
