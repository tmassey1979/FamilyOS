namespace FamilyOS.Domain.TimeIntelligence;

/// <summary>
/// Historical timing data for learning how long tasks actually take.
/// "Family OS doesn't guess how long things take. It learns from reality."
/// </summary>
public class TaskTimingRecord
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid FamilyId { get; private set; }
    public string TaskCategory { get; private set; } = string.Empty;
    public string? TaskTitleNormalized { get; private set; }
    public int DurationMinutes { get; private set; }
    public Guid CompletedByMemberId { get; private set; }
    public Guid? SourceTaskId { get; private set; }
    public DateTime RecordedAtUtc { get; private set; } = DateTime.UtcNow;

    private TaskTimingRecord() { }

    public static TaskTimingRecord Create(
        Guid familyId,
        string category,
        int durationMinutes,
        Guid completedByMemberId,
        string? titleNormalized = null,
        Guid? sourceTaskId = null)
    {
        return new TaskTimingRecord
        {
            FamilyId = familyId,
            TaskCategory = category,
            TaskTitleNormalized = titleNormalized?.ToLowerInvariant().Trim(),
            DurationMinutes = durationMinutes,
            CompletedByMemberId = completedByMemberId,
            SourceTaskId = sourceTaskId
        };
    }
}

public class TaskTimingSummary
{
    public string Category { get; set; } = string.Empty;
    public string? TitleNormalized { get; set; }
    public double AverageMinutes { get; set; }
    public int SampleCount { get; set; }
    public string Confidence => SampleCount switch
    {
        >= 20 => "High",
        >= 5 => "Medium",
        >= 2 => "Low",
        _ => "Insufficient"
    };
}
