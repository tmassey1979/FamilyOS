using FamilyOS.Domain.Common;
using FamilyOS.Domain.Tasks;
using FluentAssertions;

namespace FamilyOS.Domain.Tests.Tasks;

public class RecurrenceExtraTests
{
    private static readonly Guid FamilyId = Guid.NewGuid();
    private static readonly Guid Creator = Guid.NewGuid();

    [Fact]
    public void BiWeekly_and_Yearly_ComputeNext()
    {
        var from = new DateTime(2026, 1, 1);
        RecurrenceDefinition.ComputeNext(from, RecurrenceFrequency.BiWeekly, 1)
            .Should().Be(from.AddDays(14));
        RecurrenceDefinition.ComputeNext(from, RecurrenceFrequency.Yearly, 1)
            .Should().Be(from.AddYears(1));
    }

    [Fact]
    public void Activate_after_Deactivate_restores_next()
    {
        var def = RecurrenceDefinition.Create(FamilyId, "Trash", RecurrenceFrequency.Daily);
        def.Deactivate();
        def.IsActive.Should().BeFalse();
        def.Activate();
        def.IsActive.Should().BeTrue();
        def.NextOccurrenceUtc.Should().NotBeNull();
    }

    [Fact]
    public void SetDefaultAssignee()
    {
        var def = RecurrenceDefinition.Create(FamilyId, "Dishes", RecurrenceFrequency.Daily);
        var who = Guid.NewGuid();
        def.SetDefaultAssignee(who);
        def.DefaultAssigneeMemberId.Should().Be(who);
        def.SetDefaultAssignee(null);
        def.DefaultAssigneeMemberId.Should().BeNull();
    }

    [Fact]
    public void AdvanceSchedule_without_generate()
    {
        var start = new DateTime(2026, 3, 1);
        var def = RecurrenceDefinition.Create(
            FamilyId, "Water plants", RecurrenceFrequency.Weekly, 1, DayOfWeek.Sunday,
            firstOccurrenceUtc: start);
        def.AdvanceSchedule(start);
        def.NextOccurrenceUtc!.Value.Date.Should().Be(start.AddDays(7).Date);
    }
}
