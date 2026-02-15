using System.Text.Json.Serialization;

namespace PmsZafiro.Application.Integrations.Booking;

public record BookingPayloadDto(
    [property: JsonPropertyName("channel")] string Channel,
    [property: JsonPropertyName("event_type")] string EventType,
    [property: JsonPropertyName("external_reservation_id")] string ExternalReservationId,
    [property: JsonPropertyName("guest")] BookingGuestDto Guest,
    [property: JsonPropertyName("room_data")] BookingRoomDataDto RoomData,
    [property: JsonPropertyName("check_in")] DateTime CheckIn,
    [property: JsonPropertyName("check_out")] DateTime CheckOut,
    [property: JsonPropertyName("total_amount")] decimal TotalAmount
);

public record BookingGuestDto(
    [property: JsonPropertyName("first_name")] string FirstName,
    [property: JsonPropertyName("last_name")] string LastName,
    [property: JsonPropertyName("alias_email")] string AliasEmail
);

public record BookingRoomDataDto(
    [property: JsonPropertyName("external_room_id")] string ExternalRoomId,
    [property: JsonPropertyName("external_rate_plan_id")] string ExternalRatePlanId
);