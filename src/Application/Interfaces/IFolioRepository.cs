using PmsZafiro.Domain.Entities;

namespace PmsZafiro.Application.Interfaces;

public interface IFolioRepository
{
    Task<Folio?> GetByIdAsync(Guid id);
    Task<Folio?> GetByReservationIdAsync(Guid reservationId);
    Task AddTransactionAsync(FolioTransaction transaction);
}
