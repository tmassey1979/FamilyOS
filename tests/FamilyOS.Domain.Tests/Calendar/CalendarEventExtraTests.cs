using FamilyOS.Domain.Calendar;
using FluentAssertions;

namespace FamilyOS.Domain.Tests.Calendar;

public class CalendarEventExtraTests
{
    private static readonly Guid FamilyId = Guid.NewGuid();
    private static readonly Guid Creator = Guid.NewGuid();

    [Fact]
    public void Create_sets_window_and_title()
    {
        var start = DateTime.UtcNow.AddHours(2);
        var end = start.AddHours(1);
        var e = CalendarEvent.Create(FamilyId, "  Soccer  ", start, end, Creator, location: "Field");
        e.FamilyId.Should().Be(FamilyId);
        e.Title.Should().Be("Soccer");
        e.StartsAtUtc.Should().Be(start);
        e.EndsAtUtc.Should().Be(end);
        e.Location.Should().Be("Field");
        e.CreatedByMemberId.Should().Be(Creator);
        e.IsCancelled.Should().BeFalse();
    }

    [Fact]
    public void Create_rejects_empty_title_or_inverted_range()
    {
        var start = DateTime.UtcNow;
        var act1 = () => CalendarEvent.Create(FamilyId, " ", start, start.AddHours(1), Creator);
        act1.Should().Throw<ArgumentException>();

        var act2 = () => CalendarEvent.Create(FamilyId, "X", start.AddHours(2), start, Creator);
        act2.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void UpdateDetails_and_Cancel()
    {
        var start = DateTime.UtcNow.AddDays(1);
        var e = CalendarEvent.Create(FamilyId, "Practice", start, start.AddHours(1), Creator);
        e.UpdateDetails("Game", start.AddHours(1), start.AddHours(3), "Stadium", "Bring ball");
        e.Title.Should().Be("Game");
        e.Location.Should().Be("Stadium");
        e.Description.Should().Be("Bring ball");

        e.Cancel();
        e.IsCancelled.Should().BeTrue();
    }

    [Fact]
    public void LinkTask()
    {
        var start = DateTime.UtcNow.AddDays(1);
        var e = CalendarEvent.Create(FamilyId, "Appt", start, start.AddMinutes(30), Creator);
        var taskId = Guid.NewGuid();
        e.LinkTask(taskId);
        e.LinkedTaskId.Should().Be(taskId);
    }
}
