using FamilyOS.Domain.Common;
using FamilyOS.Domain.Conditions;
using FluentAssertions;

namespace FamilyOS.Domain.Tests.Conditions;

public class ConditionDomainTests
{
    [Fact]
    public void Create_pending_then_Satisfy()
    {
        var requestId = Guid.NewGuid();
        var familyId = Guid.NewGuid();
        var assignee = Guid.NewGuid();
        var c = Condition.Create(requestId, familyId, "Finish homework first", assignee);

        c.Status.Should().Be(ConditionStatus.Pending);
        c.Description.Should().Be("Finish homework first");
        c.AssignedToMemberId.Should().Be(assignee);

        var actor = Guid.NewGuid();
        c.Satisfy(actor);
        c.Status.Should().Be(ConditionStatus.Satisfied);
        c.SatisfiedAtUtc.Should().NotBeNull();
        c.SatisfiedByMemberId.Should().Be(actor);
    }

    [Fact]
    public void Satisfy_twice_throws()
    {
        var c = Condition.Create(Guid.NewGuid(), Guid.NewGuid(), "Clean room", Guid.NewGuid());
        c.Satisfy(Guid.NewGuid());
        var act = () => c.Satisfy(Guid.NewGuid());
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Cancel_pending()
    {
        var c = Condition.Create(Guid.NewGuid(), Guid.NewGuid(), "Optional chore", Guid.NewGuid());
        c.Cancel();
        c.Status.Should().Be(ConditionStatus.Cancelled);
    }
}
