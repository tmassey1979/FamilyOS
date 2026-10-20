using FamilyOS.Domain.Common;
using FamilyOS.Domain.Tasks;
using FluentAssertions;

namespace FamilyOS.Domain.Tests.Tasks;

public class RecurrenceDefinitionTests
{
    private static readonly Guid FamilyId = Guid.NewGuid();
    private static readonly Guid CreatorId = Guid.NewGuid();
    private static readonly Guid AssigneeId = Guid.NewGuid();

    [Fact]
    public void Create_rejects_None_frequency()
    {
        var act = () => RecurrenceDefinition.Create(FamilyId, "Trash", RecurrenceFrequency.None);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_snaps_first_occurrence_to_weekday()
    {
        var monday = new DateTime(2026, 10, 12, 0, 0, 0, DateTimeKind.Utc);
        var def = RecurrenceDefinition.Create(
            FamilyId, "Trash", RecurrenceFrequency.Weekly, 1, DayOfWeek.Thursday,
            firstOccurrenceUtc: monday);

        def.NextOccurrenceUtc.Should().NotBeNull();
        def.NextOccurrenceUtc!.Value.DayOfWeek.Should().Be(DayOfWeek.Thursday);
        def.NextOccurrenceUtc.Value.Date.Should().Be(new DateTime(2026, 10, 15));
    }

    [Fact]
    public void GenerateInstance_links_recurrence_and_advances()
    {
        var start = new DateTime(2026, 10, 15, 0, 0, 0, DateTimeKind.Utc);
        var def = RecurrenceDefinition.Create(
            FamilyId, "Trash", RecurrenceFrequency.Weekly, 1, DayOfWeek.Thursday,
            defaultAssignee: AssigneeId, category: "Chores", firstOccurrenceUtc: start);

        var first = def.GenerateInstance(CreatorId);
        first.RecurrenceDefinitionId.Should().Be(def.Id);
        first.Title.Should().Be("Trash");
        first.Category.Should().Be("Chores");
        first.AssigneeMemberId.Should().Be(AssigneeId);
        first.Status.Should().Be(FamilyTaskStatus.Assigned);
        first.DueDate.Should().NotBeNull();

        def.NextOccurrenceUtc!.Value.Date.Should().Be(start.AddDays(7).Date);
    }

    [Fact]
    public void ComputeNext_daily_respects_interval()
    {
        var from = new DateTime(2026, 1, 1);
        RecurrenceDefinition.ComputeNext(from, RecurrenceFrequency.Daily, 3)
            .Should().Be(new DateTime(2026, 1, 4));
    }

    [Fact]
    public void ComputeNext_monthly()
    {
        var from = new DateTime(2026, 1, 15);
        RecurrenceDefinition.ComputeNext(from, RecurrenceFrequency.Monthly, 1)
            .Should().Be(new DateTime(2026, 2, 15));
    }

    [Fact]
    public void GenerateInstance_rejects_inactive()
    {
        var def = RecurrenceDefinition.Create(FamilyId, "Trash", RecurrenceFrequency.Daily);
        def.Deactivate();
        var act = () => def.GenerateInstance(CreatorId);
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void DeclinePolicy_can_be_set()
    {
        var def = RecurrenceDefinition.Create(FamilyId, "Trash", RecurrenceFrequency.Weekly, dayOfWeek: DayOfWeek.Monday);
        def.SetDeclinePolicy(DeclinePolicy.OfferReassignment);
        def.DeclinePolicy.Should().Be(DeclinePolicy.OfferReassignment);
    }
}
