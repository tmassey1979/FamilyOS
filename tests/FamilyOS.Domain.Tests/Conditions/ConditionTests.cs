using FamilyOS.Domain.Common;
using FamilyOS.Domain.Conditions;
using FluentAssertions;

namespace FamilyOS.Domain.Tests.Conditions;

public class ConditionTests
{
    private static readonly Guid FamilyId = Guid.NewGuid();
    private static readonly Guid RequestId = Guid.NewGuid();
    private static readonly Guid Creator = Guid.NewGuid();

    [Fact]
    public void Create_sets_pending()
    {
        var c = Condition.Create(FamilyId, RequestId, "Bring report card", Creator);
        c.FamilyId.Should().Be(FamilyId);
        c.RequestId.Should().Be(RequestId);
        c.Description.Should().Be("Bring report card");
        c.CreatedByMemberId.Should().Be(Creator);
        c.Status.Should().Be(ConditionState.Pending);
        c.SatisfiedAtUtc.Should().BeNull();
    }

    [Fact]
    public void Create_rejects_empty_description()
    {
        var act = () => Condition.Create(FamilyId, RequestId, "  ", Creator);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Satisfy_marks_met()
    {
        var c = Condition.Create(FamilyId, RequestId, "Clean room", Creator);
        var actor = Guid.NewGuid();
        c.Satisfy(actor);
        c.Status.Should().Be(ConditionState.Met);
        c.SatisfiedByMemberId.Should().Be(actor);
        c.SatisfiedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public void Satisfy_twice_throws()
    {
        var c = Condition.Create(FamilyId, RequestId, "X", Creator);
        c.Satisfy(Guid.NewGuid());
        var act = () => c.Satisfy(Guid.NewGuid());
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Waive_marks_waived()
    {
        var c = Condition.Create(FamilyId, RequestId, "X", Creator);
        var owner = Guid.NewGuid();
        c.Waive(owner);
        c.Status.Should().Be(ConditionState.Waived);
        c.SatisfiedByMemberId.Should().Be(owner);
        c.SatisfiedAtUtc.Should().NotBeNull();
    }
}
