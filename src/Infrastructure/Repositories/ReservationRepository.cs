using Microsoft.EntityFrameworkCore;
using PmsZafiro.Application.DTOs.Reservations;
using PmsZafiro.Application.Interfaces;
using PmsZafiro.Domain.Entities;
using PmsZafiro.Domain.Enums;
using PmsZafiro.Infrastructure.Persistence;

namespace PmsZafiro.Infrastructure.Repositories;

public class ReservationRepository : IReservationRepository
{
    private readonly PmsDbContext _context;

    public ReservationRepository(PmsDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<Reservation>> GetAllAsync()
    {
        return await _context.Reservations
            .Include(r => r.Guest)
            .Include(r => r.Segments).ThenInclude(s => s.Room)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();
    }

    // --- NUEVO MÉTODO: Trae reservas + Saldo Real calculado en SQL ---
    public async Task<IEnumerable<ReservationDto>> GetReservationsWithLiveBalanceAsync()
    {
        var query = from r in _context.Reservations.AsNoTracking()
                    
                    // 1. LEFT JOIN PARA EL HUÉSPED (Evita que los bloqueos desaparezcan)
                    join g in _context.Guests on r.GuestId equals g.Id into guestGroup
                    from guest in guestGroup.DefaultIfEmpty()
                    
                    // 2. LEFT JOIN PARA EL FOLIO
                    join f in _context.Folios.OfType<GuestFolio>() on r.Id equals f.ReservationId into folioGroup
                    from folio in folioGroup.DefaultIfEmpty()
                    
                    orderby r.CreatedAt descending
                    select new ReservationDto
                    {
                        Id = r.Id,
                        Code = r.ConfirmationCode,
                        Status = r.Status.ToString(),
                        
                        // CORRECCIÓN CS0266: Asignar un Guid vacío si es null (Bloqueos)
                        MainGuestId = r.GuestId ?? Guid.Empty, 
                        
                        // Manejo seguro por si es un bloqueo y no tiene huésped asociado
                        MainGuestName = guest != null ? (guest.FirstName + " " + guest.LastName) : "Bloqueo/Mantenimiento",
                        
                        CheckIn = r.CheckIn,
                        CheckOut = r.CheckOut,
                        // Cálculo seguro de noches
                        Nights = (r.CheckOut.Date - r.CheckIn.Date).Days == 0 ? 1 : (r.CheckOut.Date - r.CheckIn.Date).Days,
                        TotalAmount = r.TotalAmount,

                        // Calcular Saldo Real (Debe - Haber)
                        Balance = folio != null ? folio.Transactions.Sum(t => 
                            (t.Type == TransactionType.Charge || t.Type == TransactionType.Expense) ? t.Amount : 
                            (t.Type == TransactionType.Payment || t.Type == TransactionType.Income) ? -t.Amount : 0) : 0,

                        // Calcular Total Pagado
                        PaidAmount = folio != null ? folio.Transactions
                            .Where(t => t.Type == TransactionType.Payment || t.Type == TransactionType.Income)
                            .Sum(t => t.Amount) : 0,

                        // Mapeo de Segmentos
                        Segments = r.Segments.Select(s => new ReservationSegmentDto 
                        {
                            RoomId = s.RoomId,
                            RoomNumber = s.Room.Number,
                            Start = s.CheckIn,
                            End = s.CheckOut
                        }).ToList()
                    };

        return await query.ToListAsync();
    }

    public async Task<Reservation?> GetByIdAsync(Guid id)
    {
        return await _context.Reservations
            .Include(r => r.Guest)
            .Include(r => r.Segments).ThenInclude(s => s.Room)
            .FirstOrDefaultAsync(r => r.Id == id);
    }

    public async Task<Reservation> CreateAsync(Reservation reservation)
    {
        await _context.Reservations.AddAsync(reservation);
        await _context.SaveChangesAsync();
        return reservation;
    }

    public async Task UpdateAsync(Reservation reservation)
    {
        _context.Reservations.Update(reservation);
        await _context.SaveChangesAsync();
    }

    public async Task<Reservation?> GetByCodeAsync(string code)
    {
        return await _context.Reservations
            .Include(r => r.Guest)
            .Include(r => r.Segments).ThenInclude(s => s.Room)
            .FirstOrDefaultAsync(r => r.ConfirmationCode == code);
    }

    public async Task ProcessCheckOutAsync(Reservation reservation, Room? room, Folio folio)
    {
        var strategy = _context.Database.CreateExecutionStrategy();

        await strategy.ExecuteAsync(async () =>
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                _context.Reservations.Update(reservation);
                _context.Folios.Update(folio);

                if (room != null)
                {
                    _context.Rooms.Update(room);
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        });
    }

    public async Task<IEnumerable<Reservation>> GetActiveReservationsByRoomAsync(Guid roomId)
    {
        return await _context.Reservations
            .Include(r => r.Segments)
            .Where(r => r.Segments.Any(s => s.RoomId == roomId) &&
                       (r.Status == ReservationStatus.Confirmed || r.Status == ReservationStatus.CheckedIn))
            .ToListAsync();
    }
    
    public async Task ProcessCheckInAsync(Reservation reservation, Room room, GuestFolio newFolio)
    {
        var strategy = _context.Database.CreateExecutionStrategy();

        await strategy.ExecuteAsync(async () =>
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                _context.Reservations.Update(reservation);
                _context.Rooms.Update(room);
                await _context.Folios.AddAsync(newFolio);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        });
    }
}