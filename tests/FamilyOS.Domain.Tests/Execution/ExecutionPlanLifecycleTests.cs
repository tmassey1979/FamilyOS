using FamilyOS.Domain.Common;
using FamilyOS.Domain.Execution;
using FluentAssertions;

namespace FamilyOS.Domain.Tests.Execution;

public class ExecutionPlanLifecycleTests
{
    private static readonly Guid FamilyId = Guid.NewGuid();
    private static readonly Guid RequestId = Guid.NewGuid();
    private static readonly Guid Creator = Guid.NewGuid();

    [Fact]
    public void Commit_requires_selected_item()
    {
        var plan = ExecutionPlan.Create(FamilyId, RequestId, Creator);
        plan.Status.Should().Be(ExecutionPlanStatus.Proposed);
        var act = () => plan.Commit(Creator);
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void AddItem_Commit_Complete()
    {
        var plan = ExecutionPlan.Create(FamilyId, RequestId, Creator);
        var item = plan.AddItem(ExecutionItemType.Task, "Buy milk", Creator);
        item.IsSelected.Should().BeTrue();
        plan.Commit(Creator);
        plan.Status.Should().Be(ExecutionPlanStatus.Committed);
        plan.CommittedByMemberId.Should().Be(Creator);
        plan.MarkCompleted();
        plan.Status.Should().Be(ExecutionPlanStatus.Completed);
    }

    [Fact]
    public void Cannot_add_after_commit()
    {
        var plan = ExecutionPlan.Create(FamilyId, RequestId, Creator);
        plan.AddItem(ExecutionItemType.Task, "A");
        plan.Commit(Creator);
        var act = () => plan.AddItem(ExecutionItemType.CalendarEvent, "B");
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void RemoveItem_deselects()
    {
        var plan = ExecutionPlan.Create(FamilyId, RequestId, Creator);
        var item = plan.AddItem(ExecutionItemType.ShoppingItem, "Eggs");
        plan.RemoveItem(item.Id);
        item.IsSelected.Should().BeFalse();
        item.Select();
        item.IsSelected.Should().BeTrue();
        item.LinkResult(Guid.NewGuid());
        item.ResultingEntityId.Should().NotBeNull();
    }
}
