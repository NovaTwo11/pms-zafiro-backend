namespace PmsZafiro.Application.DTOs.Dashboard;

public class RoomStatusCountsDto
{
    public int Clean { get; set; }
    public int Dirty { get; set; }
    public int Maintenance { get; set; }
    public int Occupied { get; set; }
}