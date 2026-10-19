using FamilyOS.Domain.Common;
using FamilyOS.Domain.Conditions;
using FluentAssertions;

namespace FamilyOS.Domain.Tests.Conditions;

public class ConditionDomainTests
{
    [Fact]
    public void Create_pending_then_Satisfy()
    {
        var familyId = Guid.NewGuid();
        var requestId = Guid.NewGuid();
        var c = Condition.Create(
            familyId,
            "Finish homework first",
            ConditionType.Manual,
            requestId: requestId,
            description: "Before screen time");

        c.Status.Should().Be(ConditionStatus.Pending);
        c.Name.Should().Be("Finish homework first");
        c.RequestId.Should().Be(requestId);

        var actor = Guid.NewGuid();
        c.Satisfy(actor);
        c.Status.Should().Be(ConditionStatus.Satisfied);
        c.SatisfiedAtUtc.Should().NotBeNull();
        c.SatisfiedByMemberId.Should().Be(actor);
    }

    [Fact]
    public void Satisfy_twice_throws()
    {
        var c = Condition.Create(Guid.NewGuid(), "Clean room", ConditionType.Manual);
        c.Satisfy(Guid.NewGuid());
        var act = () => c.Satisfy(Guid.NewGuid());
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Fail_and_Cancel()
    {
        var c1 = Condition.Create(Guid.NewGuid(), "Timed", ConditionType.TimeWindow);
        c1.Fail("Missed deadline");
        c1.Status.Should().Be(ConditionStatus.Failed);

        var c2 = Condition.Create(Guid.NewGuid(), "Optional", ConditionType.Custom);
        c2.Cancel();
        c2.Status.Should().Be(ConditionStatus.Cancelled);
    }

    [Fact]
    public void Expire_only_when_pending()
    {
        var c = Condition.Create(Guid.NewGuid(), "Window", ConditionType.TimeWindow);
        c.Expire();
        c.Status.Should().Be(ConditionStatus.Expired);
        c.Expire();
        c.Status.Should().Be(ConditionStatus.Expired);
    }
}
