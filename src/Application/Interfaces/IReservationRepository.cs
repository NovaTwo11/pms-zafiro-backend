using PmsZafiro.Application.DTOs.Reservations; // Importante para los DTOs
using PmsZafiro.Domain.Entities;

namespace PmsZafiro.Application.Interfaces;

public interface IReservationRepository
{
    Task<IEnumerable<Reservation>> GetAllAsync();
    Task<Reservation?> GetByIdAsync(Guid id);
    Task<Reservation?> GetByCodeAsync(string code);
    
    Task<Reservation> CreateAsync(Reservation reservation);
    Task UpdateAsync(Reservation reservation);
    
    Task ProcessCheckOutAsync(Reservation reservation, Room? room, Folio folio);
    
    Task<IEnumerable<Reservation>> GetActiveReservationsByRoomAsync(Guid roomId);
    
    Task ProcessCheckInAsync(Reservation reservation, Room room, GuestFolio newFolio);

    // --- NUEVO MÉTODO PARA EL CRONOGRAMA ---
    Task<IEnumerable<ReservationDto>> GetReservationsWithLiveBalanceAsync();
}