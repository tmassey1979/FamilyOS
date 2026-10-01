using FamilyOS.Domain.Common;
using FamilyOS.Domain.Execution;
using FluentAssertions;

namespace FamilyOS.Domain.Tests.Execution;

public class ExecutionPlanTests
{
    private static readonly Guid FamilyId = Guid.NewGuid();
    private static readonly Guid RequestId = Guid.NewGuid();
    private static readonly Guid MemberId = Guid.NewGuid();

    [Fact]
    public void Create_proposed_plan_and_add_items()
    {
        var plan = ExecutionPlan.Create(FamilyId, RequestId, MemberId);
        plan.Status.Should().Be(ExecutionPlanStatus.Proposed);

        var item = plan.AddItem(ExecutionItemType.Task, "Drive Mia", MemberId);
        item.IsSelected.Should().BeTrue();
        plan.Items.Should().HaveCount(1);
    }

    [Fact]
    public void Commit_requires_selected_items()
    {
        var plan = ExecutionPlan.Create(FamilyId, RequestId, MemberId);
        var act = () => plan.Commit(MemberId);
        act.Should().Throw<InvalidOperationException>();

        plan.AddItem(ExecutionItemType.CalendarEvent, "Pickup");
        plan.Commit(MemberId);
        plan.Status.Should().Be(ExecutionPlanStatus.Committed);
        plan.CommittedByMemberId.Should().Be(MemberId);
    }

    [Fact]
    public void Cannot_modify_committed_plan()
    {
        var plan = ExecutionPlan.Create(FamilyId, RequestId, MemberId);
        plan.AddItem(ExecutionItemType.Task, "T1");
        plan.Commit(MemberId);
        var act = () => plan.AddItem(ExecutionItemType.Task, "T2");
        act.Should().Throw<InvalidOperationException>();
    }
}
