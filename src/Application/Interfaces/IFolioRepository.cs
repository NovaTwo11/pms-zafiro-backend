using PmsZafiro.Domain.Entities;

namespace PmsZafiro.Application.Interfaces;

public interface IFolioRepository
{
    Task<Folio?> GetByIdAsync(Guid id);
    Task<GuestFolio?> GetByReservationIdAsync(Guid reservationId);
    Task<IEnumerable<Folio>> GetAllAsync();
    
    Task AddAsync(Folio folio);
    Task AddTransactionAsync(FolioTransaction transaction);
    Task UpdateAsync(Folio folio);
    
    // --- NUEVO MÉTODO CRÍTICO ---
    // Obtiene el saldo calculándolo directamente en la DB para evitar caché de EF Core
    Task<decimal> GetFolioBalanceAsync(Guid folioId);
    
    Task<IEnumerable<GuestFolio>> GetActiveGuestFoliosAsync();
    Task<IEnumerable<ExternalFolio>> GetActiveExternalFoliosAsync();
    Task DeleteAsync(Folio folio);
    
    
}