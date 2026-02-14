using System;
using System.Collections.Generic;

namespace PmsZafiro.Application.DTOs.Reservations;

public class ReservationDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int StatusStep { get; set; } // 1: Reservada, 2: Confirmada, 3: Hospedado, 4: Finalizada
    
    public string RoomId { get; set; } = string.Empty;
    public string RoomName { get; set; } = string.Empty;
    
    public Guid MainGuestId { get; set; }
    public string MainGuestName { get; set; } = string.Empty;
    
    public DateTime CheckIn { get; set; }
    public DateTime CheckOut { get; set; }
    public int Nights { get; set; }
    public int Adults { get; set; }
    public int Children { get; set; }
    public string Origin { get; set; } = "Directo";
    public DateTime CreatedDate { get; set; }
    public string Notes { get; set; } = string.Empty;
    
    public decimal TotalAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal Balance { get; set; }
    
    public Guid? FolioId { get; set; }

    public List<ReservationSegmentDto> Segments { get; set; } = new();
    public List<GuestDetailDto> Guests { get; set; } = new();
    public List<FolioItemDto> FolioItems { get; set; } = new();
}

public class GuestDetailDto
{
    public string Id { get; set; } = string.Empty;
    public string PrimerNombre { get; set; } = string.Empty;
    public string SegundoNombre { get; set; } = string.Empty;
    public string PrimerApellido { get; set; } = string.Empty;
    public string SegundoApellido { get; set; } = string.Empty;
    public string Correo { get; set; } = string.Empty;
    public string Telefono { get; set; } = string.Empty;
    public string PaisOrigen { get; set; } = string.Empty;
    public string CiudadOrigen { get; set; } = string.Empty;
    public string PaisResidencia { get; set; } = string.Empty;
    public string CiudadResidencia { get; set; } = string.Empty;
    public string DireccionResidencia { get; set; } = string.Empty;
    public string TipoId { get; set; } = string.Empty;
    public string NumeroId { get; set; } = string.Empty;
    public string Nacionalidad { get; set; } = string.Empty;
    public string Ocupacion { get; set; } = string.Empty;
    public DateTime? FechaNacimiento { get; set; }
    public bool EsTitular { get; set; }
    public bool IsSigned { get; set; }
}

public class FolioItemDto
{
    public Guid Id { get; set; }
    public string Date { get; set; } = string.Empty;
    public string Concept { get; set; } = string.Empty;
    public int Qty { get; set; }
    public decimal Price { get; set; }
    public decimal Total { get; set; }
}