using PmsZafiro.Domain.Entities;

namespace PmsZafiro.Application.Interfaces;

public interface IReservationRepository
{
    Task<IEnumerable<Reservation>> GetAllAsync();
    Task<Reservation?> GetByIdAsync(Guid id);
    Task<Reservation?> GetByCodeAsync(string code);
    
    Task<Reservation> CreateAsync(Reservation reservation);
    Task UpdateAsync(Reservation reservation);
    
    // Método CRÍTICO para el arreglo de Check-out
    Task ProcessCheckOutAsync(Reservation reservation, Room? room, Folio folio);
    
    Task<IEnumerable<Reservation>> GetActiveReservationsByRoomAsync(Guid roomId);
    
    Task ProcessCheckInAsync(Reservation reservation, Room room, GuestFolio newFolio);
}