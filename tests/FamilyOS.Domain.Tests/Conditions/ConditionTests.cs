using FamilyOS.Domain.Common;
using FamilyOS.Domain.Conditions;
using FluentAssertions;

namespace FamilyOS.Domain.Tests.Conditions;

public class ConditionTests
{
    private static readonly Guid FamilyId = Guid.NewGuid();
    private static readonly Guid MemberId = Guid.NewGuid();

    [Fact]
    public void Satisfy_from_Pending()
    {
        var c = Condition.Create(FamilyId, "Transport confirmed", ConditionType.Availability);
        c.Satisfy(MemberId);
        c.Status.Should().Be(ConditionStatus.Satisfied);
        c.SatisfiedByMemberId.Should().Be(MemberId);
        c.SatisfiedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public void Cannot_satisfy_twice()
    {
        var c = Condition.Create(FamilyId, "Chores done", ConditionType.Task);
        c.Satisfy(MemberId);
        var act = () => c.Satisfy(MemberId);
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Fail_and_Expire()
    {
        var c = Condition.Create(FamilyId, "Deadline", ConditionType.Deadline, deadline: DateTime.UtcNow.AddHours(-1));
        c.Fail("Missed window");
        c.Status.Should().Be(ConditionStatus.Failed);

        var c2 = Condition.Create(FamilyId, "Other", ConditionType.Event);
        c2.Expire();
        c2.Status.Should().Be(ConditionStatus.Expired);
    }

    [Fact]
    public void Cancel_from_pending()
    {
        var c = Condition.Create(FamilyId, "Optional", ConditionType.Custom);
        c.Cancel();
        c.Status.Should().Be(ConditionStatus.Cancelled);
    }
}
