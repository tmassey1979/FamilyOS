namespace FamilyOS.Domain.TimeIntelligence;

/// <summary>
/// Planned vs actual timing summary for a task (never overwrites planned with actual).
/// </summary>
public class TaskTimingRecord
{
    public Guid TaskId { get; private set; }
    public int? PlannedMinutes { get; private set; }
    public int? ActualMinutes { get; private set; }
    public DateTime? PlannedStartUtc { get; private set; }
    public DateTime? PlannedEndUtc { get; private set; }
    public DateTime? ActualStartUtc { get; private set; }
    public DateTime? ActualEndUtc { get; private set; }

    private TaskTimingRecord() { }

    public static TaskTimingRecord FromTask(
        Guid taskId,
        int? plannedMinutes,
        int? actualMinutes,
        DateTime? plannedStart = null,
        DateTime? plannedEnd = null,
        DateTime? actualStart = null,
        DateTime? actualEnd = null)
    {
        return new TaskTimingRecord
        {
            TaskId = taskId,
            PlannedMinutes = plannedMinutes,
            ActualMinutes = actualMinutes,
            PlannedStartUtc = plannedStart,
            PlannedEndUtc = plannedEnd,
            ActualStartUtc = actualStart,
            ActualEndUtc = actualEnd
        };
    }
}

public class TaskTimingSummary
{
    public int TaskCount { get; init; }
    public double? AveragePlannedMinutes { get; init; }
    public double? AverageActualMinutes { get; init; }
    public double? AverageVarianceMinutes { get; init; }
}
