using FamilyOS.Domain.Conditions;
using FluentAssertions;

namespace FamilyOS.Domain.Tests.Conditions;

public class ConditionDomainTests
{
    [Fact]
    public void Create_pending_then_Satisfy()
    {
        var requestId = Guid.NewGuid();
        var owner = Guid.NewGuid();
        var c = Condition.Create(requestId, "Finish homework first", ownerMemberId: owner);

        c.IsSatisfied.Should().BeFalse();
        c.Description.Should().Be("Finish homework first");

        c.Satisfy(satisfiedByMemberId: Guid.NewGuid());
        c.IsSatisfied.Should().BeTrue();
        c.SatisfiedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public void Satisfy_is_idempotent()
    {
        var c = Condition.Create(Guid.NewGuid(), "Clean room", Guid.NewGuid());
        c.Satisfy(Guid.NewGuid());
        var first = c.SatisfiedAtUtc;
        c.Satisfy(Guid.NewGuid()); // second call should not throw
        c.IsSatisfied.Should().BeTrue();
        c.SatisfiedAtUtc.Should().Be(first);
    }
}
