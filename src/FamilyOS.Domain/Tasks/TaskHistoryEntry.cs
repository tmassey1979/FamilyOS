namespace FamilyOS.Domain.Tasks;

public class TaskHistoryEntry
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid TaskId { get; private set; }
    public Common.FamilyTaskStatus Status { get; private set; }
    public Guid ActorMemberId { get; private set; }
    public string Detail { get; private set; } = string.Empty;
    public DateTime TimestampUtc { get; private set; } = DateTime.UtcNow;

    private TaskHistoryEntry() { }

    public static TaskHistoryEntry Create(Guid taskId, Common.FamilyTaskStatus status, Guid actorMemberId, string detail)
    {
        return new TaskHistoryEntry
        {
            TaskId = taskId,
            Status = status,
            ActorMemberId = actorMemberId,
            Detail = detail
        };
    }
}
