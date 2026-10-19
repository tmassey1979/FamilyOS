using FamilyOS.Domain.Calendar;
using FamilyOS.Domain.Common;
using FluentAssertions;

namespace FamilyOS.Domain.Tests.Calendar;

public class CalendarEventExtraTests
{
    private static readonly Guid FamilyId = Guid.NewGuid();

    [Fact]
    public void Create_sets_window_and_title()
    {
        var start = DateTime.UtcNow.AddHours(2);
        var end = start.AddHours(1);
        var e = CalendarEvent.Create(FamilyId, "  Soccer  ", start, end, location: "Field");
        e.FamilyId.Should().Be(FamilyId);
        e.Title.Should().Be("Soccer");
        e.StartUtc.Should().Be(start);
        e.EndUtc.Should().Be(end);
        e.Location.Should().Be("Field");
        e.Status.Should().Be(CalendarEventStatus.Scheduled);
    }

    [Fact]
    public void Create_rejects_empty_title_or_inverted_range()
    {
        var start = DateTime.UtcNow;
        var act1 = () => CalendarEvent.Create(FamilyId, " ", start, start.AddHours(1));
        act1.Should().Throw<ArgumentException>();

        var act2 = () => CalendarEvent.Create(FamilyId, "X", start.AddHours(2), start);
        act2.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void UpdateDetails_Reschedule_Cancel()
    {
        var start = DateTime.UtcNow.AddDays(1);
        var e = CalendarEvent.Create(FamilyId, "Practice", start, start.AddHours(1));
        e.UpdateDetails("Game", "Stadium", "Bring ball");
        e.Title.Should().Be("Game");
        e.Location.Should().Be("Stadium");
        e.Description.Should().Be("Bring ball");

        var newStart = start.AddHours(2);
        e.Reschedule(newStart, newStart.AddHours(2));
        e.StartUtc.Should().Be(newStart);

        e.Cancel();
        e.Status.Should().Be(CalendarEventStatus.Cancelled);
    }

    [Fact]
    public void LinkTask_and_Complete()
    {
        var start = DateTime.UtcNow.AddDays(1);
        var e = CalendarEvent.Create(FamilyId, "Appt", start, start.AddMinutes(30));
        var taskId = Guid.NewGuid();
        e.LinkTask(taskId);
        e.LinkedTaskId.Should().Be(taskId);

        e.Complete();
        e.Status.Should().Be(CalendarEventStatus.Completed);
    }
}
