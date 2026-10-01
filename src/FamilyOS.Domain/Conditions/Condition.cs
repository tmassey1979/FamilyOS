using FamilyOS.Domain.Common;

namespace FamilyOS.Domain.Conditions;

/// <summary>
/// Reusable condition engine entity. Not hard-coded for requests only.
/// </summary>
public class Condition : Entity
{
    public Guid FamilyId { get; private set; }
    public Guid? RequestId { get; private set; }
    public Guid? TaskId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public ConditionType Type { get; private set; }
    public ConditionStatus Status { get; private set; } = ConditionStatus.Pending;
    public ConditionLogic LogicGroup { get; private set; } = ConditionLogic.All;
    public int GroupOrder { get; private set; }
    public DateTime? DeadlineUtc { get; private set; }
    public string? MetadataJson { get; private set; }
    public Guid? SatisfiedByMemberId { get; private set; }
    public DateTime? SatisfiedAtUtc { get; private set; }

    private Condition() { }

    public static Condition Create(
        Guid familyId,
        string name,
        ConditionType type,
        Guid? requestId = null,
        Guid? taskId = null,
        string? description = null,
        DateTime? deadline = null,
        ConditionLogic logic = ConditionLogic.All,
        int groupOrder = 0)
    {
        return new Condition
        {
            FamilyId = familyId,
            Name = name.Trim(),
            Type = type,
            RequestId = requestId,
            TaskId = taskId,
            Description = description,
            DeadlineUtc = deadline,
            LogicGroup = logic,
            GroupOrder = groupOrder
        };
    }

    public void Satisfy(Guid memberId)
    {
        if (Status != ConditionStatus.Pending)
            throw new InvalidOperationException($"Condition is already {Status}.");
        Status = ConditionStatus.Satisfied;
        SatisfiedByMemberId = memberId;
        SatisfiedAtUtc = DateTime.UtcNow;
        MarkUpdated();
    }

    public void Fail(string? reason = null)
    {
        if (Status != ConditionStatus.Pending)
            throw new InvalidOperationException($"Condition is already {Status}.");
        Status = ConditionStatus.Failed;
        Description = reason ?? Description;
        MarkUpdated();
    }

    public void Expire()
    {
        if (Status == ConditionStatus.Pending)
        {
            Status = ConditionStatus.Expired;
            MarkUpdated();
        }
    }

    public void Cancel()
    {
        Status = ConditionStatus.Cancelled;
        MarkUpdated();
    }
}
