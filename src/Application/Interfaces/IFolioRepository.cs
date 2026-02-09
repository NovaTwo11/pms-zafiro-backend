using PmsZafiro.Domain.Entities;

namespace PmsZafiro.Application.Interfaces;

public interface IFolioRepository
{
    Task<Folio?> GetByIdAsync(Guid id);
    Task<GuestFolio?> GetByReservationIdAsync(Guid reservationId);
    
    // Método necesario para listar Externos (Corrige error CS1061 GetAllAsync)
    Task<IEnumerable<Folio>> GetAllAsync(); 

    // Renombramos CreateAsync -> AddAsync para que coincida con el Controller (Corrige error CS1061 AddAsync)
    Task AddAsync(Folio folio); 
    
    Task AddTransactionAsync(FolioTransaction transaction);
    Task UpdateAsync(Folio folio);
    
    Task<IEnumerable<GuestFolio>> GetActiveGuestFoliosAsync();
    // Nota: Si no tienes implementado GetActiveExternalFoliosAsync en el repo, puedes quitar esta línea
    // y usar la lógica de filtrado en memoria que puse en el Controller con GetAllAsync.
    // Si la dejas, debes implementarla en FolioRepository.cs
    Task<IEnumerable<ExternalFolio>> GetActiveExternalFoliosAsync();
}