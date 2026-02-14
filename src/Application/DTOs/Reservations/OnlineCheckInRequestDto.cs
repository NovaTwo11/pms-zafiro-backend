namespace PmsZafiro.Application.DTOs.Reservations;

public class OnlineCheckInRequestDto
{
    public OnlineCheckInGuestDto MainGuest { get; set; } = null!;
    public List<OnlineCheckInGuestDto> Companions { get; set; } = new();
}

public class OnlineCheckInGuestDto
{
    public string PrimerNombre { get; set; } = "";
    public string SegundoNombre { get; set; } = "";
    public string PrimerApellido { get; set; } = "";
    public string SegundoApellido { get; set; } = "";
    public string TipoId { get; set; } = "";
    public string NumeroId { get; set; } = "";
    public string FechaCumpleanos { get; set; } = "";
    public string Nacionalidad { get; set; } = "";
    public string Telefono { get; set; } = "";
    public string Correo { get; set; } = "";
    public string CiudadOrigen { get; set; } = "";
}