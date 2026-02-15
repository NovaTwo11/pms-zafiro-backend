using PmsZafiro.Domain.Enums;

namespace PmsZafiro.Application.DTOs.Channels;

public class ChannelMappingDto
{
    public string RoomCategory { get; set; } = string.Empty;
    public string ExternalRoomId { get; set; } = string.Empty;
    public BookingChannel Channel { get; set; }
}