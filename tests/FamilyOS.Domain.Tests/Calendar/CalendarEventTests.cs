using FamilyOS.Domain.Calendar;
using FluentAssertions;

namespace FamilyOS.Domain.Tests.Calendar;

public class CalendarEventTests
{
    private static readonly Guid FamilyId = Guid.NewGuid();

    [Fact]
    public void Create_sets_core_fields()
    {
        var start = DateTime.UtcNow.AddHours(1);
        var end = start.AddHours(2);
        var e = CalendarEvent.Create(FamilyId, "Dentist", start, end, location: "Clinic");

        e.FamilyId.Should().Be(FamilyId);
        e.Title.Should().Be("Dentist");
        e.StartUtc.Should().Be(start);
        e.EndUtc.Should().Be(end);
        e.Location.Should().Be("Clinic");
        e.IsCancelled.Should().BeFalse();
    }

    [Fact]
    public void Create_rejects_empty_title()
    {
        var start = DateTime.UtcNow;
        var act = () => CalendarEvent.Create(FamilyId, "  ", start, start.AddHours(1));
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_rejects_end_before_start()
    {
        var start = DateTime.UtcNow;
        var act = () => CalendarEvent.Create(FamilyId, "X", start, start.AddMinutes(-1));
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Cancel_sets_flag_and_timestamp()
    {
        var start = DateTime.UtcNow.AddDays(1);
        var e = CalendarEvent.Create(FamilyId, "Game", start, start.AddHours(1));
        e.Cancel();
        e.IsCancelled.Should().BeTrue();
        e.CancelledAtUtc.Should().NotBeNull();
    }

    [Fact]
    public void Reschedule_updates_window()
    {
        var start = DateTime.UtcNow.AddDays(1);
        var e = CalendarEvent.Create(FamilyId, "Practice", start, start.AddHours(1));
        var newStart = start.AddDays(1);
        e.Reschedule(newStart, newStart.AddHours(2));
        e.StartUtc.Should().Be(newStart);
        e.EndUtc.Should().Be(newStart.AddHours(2));
    }
}
