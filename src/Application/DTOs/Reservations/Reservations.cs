namespace PmsZafiro.Application.DTOs.Reservations
{
    public class SplitSegmentDto
    {
        public DateTime SplitDate { get; set; }
        public Guid NewRoomId { get; set; }
    }

    public class MoveSegmentDto
    {
        public Guid NewRoomId { get; set; }
        public DateTime NewStartDate { get; set; }
        public DateTime NewEndDate { get; set; }
    }

    public class MergeSegmentsDto
    {
        public Guid SegmentToKeepId { get; set; }
        public Guid SegmentToRemoveId { get; set; }
    }
}