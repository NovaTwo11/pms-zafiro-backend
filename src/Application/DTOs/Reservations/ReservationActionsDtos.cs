using System;

namespace PmsZafiro.Application.DTOs.Reservations;

public class SplitSegmentDto
{
    public int SegmentIndex { get; set; }
    public DateTime SplitDate { get; set; }
    public Guid? NewRoomId { get; set; } // Opcional: Si se mueve a otra hab al instante
}

public class MoveSegmentDto
{
    public Guid NewRoomId { get; set; }
}