using PmsZafiro.Domain.Entities;

namespace PmsZafiro.Application.Interfaces;

public interface IFolioRepository
{
    Task<Folio?> GetByIdAsync(Guid id);
    Task<GuestFolio?> GetByReservationIdAsync(Guid reservationId);
    Task CreateAsync(Folio folio);
    Task AddTransactionAsync(FolioTransaction transaction);
    Task UpdateAsync(Folio folio); // Para actualizar estado
    
    // Nuevos métodos para listas
    Task<IEnumerable<GuestFolio>> GetActiveGuestFoliosAsync();
    Task<IEnumerable<ExternalFolio>> GetActiveExternalFoliosAsync();
}
