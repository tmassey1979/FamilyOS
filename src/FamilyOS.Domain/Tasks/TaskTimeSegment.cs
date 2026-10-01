namespace FamilyOS.Domain.Tasks;

/// <summary>
/// Records active work time segments (start/pause/resume).
/// Planned time is never overwritten by actual time.
/// </summary>
public class TaskTimeSegment
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid TaskId { get; private set; }
    public Guid MemberId { get; private set; }
    public DateTime StartedAtUtc { get; private set; }
    public DateTime? EndedAtUtc { get; private set; }

    private TaskTimeSegment() { }

    public static TaskTimeSegment Start(Guid taskId, Guid memberId)
    {
        return new TaskTimeSegment
        {
            TaskId = taskId,
            MemberId = memberId,
            StartedAtUtc = DateTime.UtcNow
        };
    }

    public void End(Guid memberId)
    {
        if (EndedAtUtc.HasValue)
            throw new InvalidOperationException("Segment already ended.");
        if (MemberId != memberId)
            throw new UnauthorizedAccessException("Only the member who started the segment can end it.");
        EndedAtUtc = DateTime.UtcNow;
    }

    public int DurationMinutes => EndedAtUtc.HasValue
        ? (int)(EndedAtUtc.Value - StartedAtUtc).TotalMinutes
        : (int)(DateTime.UtcNow - StartedAtUtc).TotalMinutes;
}
