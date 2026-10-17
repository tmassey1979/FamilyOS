using FamilyOS.Domain.Common;
using FamilyOS.Domain.Conditions;
using FluentAssertions;

namespace FamilyOS.Domain.Tests.Conditions;

public class ConditionLifecycleTests
{
    private static Condition Pending() =>
        Condition.Create(Guid.NewGuid(), "Chores done", ConditionType.Task,
            requestId: Guid.NewGuid(), description: "Finish kitchen");

    [Fact]
    public void Satisfy_sets_member_and_time()
    {
        var c = Pending();
        var who = Guid.NewGuid();
        c.Satisfy(who);
        c.Status.Should().Be(ConditionStatus.Satisfied);
        c.SatisfiedByMemberId.Should().Be(who);
        c.SatisfiedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public void Satisfy_twice_throws()
    {
        var c = Pending();
        c.Satisfy(Guid.NewGuid());
        var act = () => c.Satisfy(Guid.NewGuid());
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Fail_and_Expire_and_Cancel()
    {
        var a = Pending();
        a.Fail("missed deadline");
        a.Status.Should().Be(ConditionStatus.Failed);

        var b = Pending();
        b.Expire();
        b.Status.Should().Be(ConditionStatus.Expired);

        var c = Pending();
        c.Cancel();
        c.Status.Should().Be(ConditionStatus.Cancelled);
    }

    [Fact]
    public void Expire_noop_when_already_satisfied()
    {
        var c = Pending();
        c.Satisfy(Guid.NewGuid());
        c.Expire();
        c.Status.Should().Be(ConditionStatus.Satisfied);
    }
}
