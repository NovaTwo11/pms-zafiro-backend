using PmsZafiro.Domain.Entities;

namespace PmsZafiro.Application.Interfaces;

public interface IFolioRepository
{
    Task<Folio?> GetByIdAsync(Guid id);
    Task<GuestFolio?> GetByReservationIdAsync(Guid reservationId);
    Task<IEnumerable<Folio>> GetAllAsync();
    
    Task AddAsync(Folio folio);
    Task AddTransactionAsync(FolioTransaction transaction);
    Task UpdateAsync(Folio folio); // <--- CRÍTICO para el Checkout
    
    Task<IEnumerable<GuestFolio>> GetActiveGuestFoliosAsync();
    Task<IEnumerable<ExternalFolio>> GetActiveExternalFoliosAsync();
}