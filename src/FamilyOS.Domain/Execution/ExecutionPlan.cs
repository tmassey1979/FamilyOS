using FamilyOS.Domain.Common;

namespace FamilyOS.Domain.Execution;

public class ExecutionPlan : Entity
{
    public Guid FamilyId { get; private set; }
    public Guid RequestId { get; private set; }
    public Guid CreatedByMemberId { get; private set; }
    public ExecutionPlanStatus Status { get; private set; } = ExecutionPlanStatus.Draft;
    public DateTime? CommittedAtUtc { get; private set; }
    public Guid? CommittedByMemberId { get; private set; }

    private readonly List<ExecutionItem> _items = new();
    public IReadOnlyCollection<ExecutionItem> Items => _items.AsReadOnly();

    private ExecutionPlan() { }

    public static ExecutionPlan Create(Guid familyId, Guid requestId, Guid createdByMemberId)
    {
        return new ExecutionPlan
        {
            FamilyId = familyId,
            RequestId = requestId,
            CreatedByMemberId = createdByMemberId,
            Status = ExecutionPlanStatus.Proposed
        };
    }

    public ExecutionItem AddItem(ExecutionItemType type, string title, Guid? assigneeMemberId = null, string? metadataJson = null)
    {
        if (Status is ExecutionPlanStatus.Committed or ExecutionPlanStatus.Completed or ExecutionPlanStatus.Cancelled)
            throw new InvalidOperationException("Cannot modify a committed plan.");

        var item = ExecutionItem.Create(Id, type, title, assigneeMemberId, metadataJson);
        _items.Add(item);
        MarkUpdated();
        return item;
    }

    public void RemoveItem(Guid itemId)
    {
        if (Status is ExecutionPlanStatus.Committed or ExecutionPlanStatus.Completed)
            throw new InvalidOperationException("Cannot modify a committed plan.");
        var item = _items.FirstOrDefault(i => i.Id == itemId);
        if (item != null)
        {
            item.Deselect();
            MarkUpdated();
        }
    }

    public void Commit(Guid memberId)
    {
        if (Status != ExecutionPlanStatus.Proposed && Status != ExecutionPlanStatus.Draft)
            throw new InvalidOperationException($"Cannot commit plan in status {Status}.");

        var selected = _items.Where(i => i.IsSelected).ToList();
        if (!selected.Any())
            throw new InvalidOperationException("At least one execution item must be selected.");

        Status = ExecutionPlanStatus.Committed;
        CommittedAtUtc = DateTime.UtcNow;
        CommittedByMemberId = memberId;
        MarkUpdated();
    }

    public void MarkCompleted()
    {
        Status = ExecutionPlanStatus.Completed;
        MarkUpdated();
    }
}

public class ExecutionItem
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid ExecutionPlanId { get; private set; }
    public ExecutionItemType Type { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public Guid? AssigneeMemberId { get; private set; }
    public bool IsSelected { get; private set; } = true;
    public string? MetadataJson { get; private set; }
    public Guid? ResultingEntityId { get; private set; }
    public DateTime CreatedAtUtc { get; private set; } = DateTime.UtcNow;

    private ExecutionItem() { }

    public static ExecutionItem Create(Guid planId, ExecutionItemType type, string title, Guid? assignee = null, string? metadata = null)
    {
        return new ExecutionItem
        {
            ExecutionPlanId = planId,
            Type = type,
            Title = title.Trim(),
            AssigneeMemberId = assignee,
            MetadataJson = metadata,
            IsSelected = true
        };
    }

    public void Deselect() => IsSelected = false;
    public void Select() => IsSelected = true;
    public void LinkResult(Guid entityId) => ResultingEntityId = entityId;
}
