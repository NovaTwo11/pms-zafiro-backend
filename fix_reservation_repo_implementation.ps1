# fix_reservation_repo_implementation.ps1
$ErrorActionPreference = "Stop"

function Write-File {
    param([string]$Path, [string]$Content)
    $Dir = [System.IO.Path]::GetDirectoryName($Path)
    if (!(Test-Path $Dir)) { New-Item -ItemType Directory -Force -Path $Dir | Out-Null }
    [System.IO.File]::WriteAllText($Path, $Content)
    Write-Host "OK: $Path updated." -ForegroundColor Green
}

Write-Host "Fixing ReservationRepository implementation..." -ForegroundColor Cyan

$repoContent = @"
using Microsoft.EntityFrameworkCore;
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
            .Include(r => r.Room)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();
    }

    public async Task<Reservation?> GetByIdAsync(Guid id)
    {
        return await _context.Reservations
            .Include(r => r.Guest)
            .Include(r => r.Room)
            .FirstOrDefaultAsync(r => r.Id == id);
    }

    // ✅ IMPLEMENTACIÓN FALTANTE 1: CreateAsync
    public async Task<Reservation> CreateAsync(Reservation reservation)
    {
        await _context.Reservations.AddAsync(reservation);
        await _context.SaveChangesAsync();
        return reservation;
    }

    // ✅ IMPLEMENTACIÓN FALTANTE 2: UpdateAsync
    public async Task UpdateAsync(Reservation reservation)
    {
        _context.Reservations.Update(reservation);
        await _context.SaveChangesAsync();
    }

    // ✅ IMPLEMENTACIÓN FALTANTE 3: GetByCodeAsync
    public async Task<Reservation?> GetByCodeAsync(string code)
    {
        return await _context.Reservations
            .Include(r => r.Guest)
            .Include(r => r.Room)
            .FirstOrDefaultAsync(r => r.ConfirmationCode == code);
    }

    // ✅ IMPLEMENTACIÓN FALTANTE 4: ProcessCheckOutAsync (Transaccional)
    public async Task ProcessCheckOutAsync(Reservation reservation, Room room, Folio folio)
    {
        // EF Core maneja esto como una transacción implícita al llamar SaveChanges una sola vez
        _context.Reservations.Update(reservation);
        _context.Rooms.Update(room);
        _context.Folios.Update(folio);
        
        await _context.SaveChangesAsync();
    }
}
"@

Write-File "src/Infrastructure/Repositories/ReservationRepository.cs" $repoContent
Write-Host "Repository fixed. Run 'dotnet build' now." -ForegroundColor Yellow