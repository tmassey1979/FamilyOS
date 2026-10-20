using FamilyOS.Domain.Conditions;
using FluentAssertions;

namespace FamilyOS.Domain.Tests.Conditions;

public class ConditionLifecycleTests
{
    private static readonly Guid FamilyId = Guid.NewGuid();
    private static readonly Guid Actor = Guid.NewGuid();

    [Fact]
    public void Full_lifecycle_pending_satisfy()
    {
        var c = Condition.Create(FamilyId, "Gate", ConditionType.Manual, logic: ConditionLogic.All, groupOrder: 1);
        c.Status.Should().Be(ConditionStatus.Pending);
        c.LogicGroup.Should().Be(ConditionLogic.All);
        c.GroupOrder.Should().Be(1);

        c.Satisfy(Actor);
        c.Status.Should().Be(ConditionStatus.Satisfied);
    }

    [Fact]
    public void Fail_then_cannot_satisfy()
    {
        var c = Condition.Create(FamilyId, "Gate", ConditionType.Approval);
        c.Fail();
        var act = () => c.Satisfy(Actor);
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Deadline_is_stored()
    {
        var deadline = DateTime.UtcNow.AddDays(2);
        var c = Condition.Create(FamilyId, "By Friday", ConditionType.TimeWindow, deadline: deadline);
        c.DeadlineUtc.Should().Be(deadline);
    }
}
