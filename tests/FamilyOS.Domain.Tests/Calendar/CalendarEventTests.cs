using FamilyOS.Domain.Calendar;
using FluentAssertions;

namespace FamilyOS.Domain.Tests.Calendar;

public class CalendarEventTests
{
    private static readonly Guid FamilyId = Guid.NewGuid();
    private static readonly Guid Creator = Guid.NewGuid();

    [Fact]
    public void Create_sets_core_fields()
    {
        var start = DateTime.UtcNow.AddHours(2);
        var end = start.AddHours(1);
        var ev = CalendarEvent.Create(FamilyId, "Soccer", start, Creator, end, location: "Park");

        ev.FamilyId.Should().Be(FamilyId);
        ev.Title.Should().Be("Soccer");
        ev.StartUtc.Should().Be(start);
        ev.EndUtc.Should().Be(end);
        ev.CreatedByMemberId.Should().Be(Creator);
        ev.Location.Should().Be("Park");
        ev.IsCancelled.Should().BeFalse();
        ev.ExternalProvider.Should().BeNull();
    }

    [Fact]
    public void Update_and_Cancel()
    {
        var start = DateTime.UtcNow;
        var ev = CalendarEvent.Create(FamilyId, "Old", start, Creator);
        ev.Update("New title", start.AddHours(1), start.AddHours(2), false, "Home", "notes");
        ev.Title.Should().Be("New title");
        ev.Location.Should().Be("Home");
        ev.Description.Should().Be("notes");

        ev.Cancel();
        ev.IsCancelled.Should().BeTrue();
    }

    [Fact]
    public void CreateFromExternal_and_LinkExternal()
    {
        var start = DateTime.UtcNow;
        var ev = CalendarEvent.CreateFromExternal(
            FamilyId, Creator, "google", "g-evt-1", "primary",
            "Imported", start, start.AddHours(1), false, null, null);

        ev.ExternalProvider.Should().Be("google");
        ev.ExternalEventId.Should().Be("g-evt-1");
        ev.ExternalCalendarId.Should().Be("primary");

        var local = CalendarEvent.Create(FamilyId, "Local", start, Creator);
        local.LinkExternal("google", "g-2", "primary");
        local.ExternalEventId.Should().Be("g-2");
    }

    [Fact]
    public void ApplyExternalUpdate_clears_cancelled()
    {
        var start = DateTime.UtcNow;
        var ev = CalendarEvent.Create(FamilyId, "X", start, Creator);
        ev.Cancel();
        ev.ApplyExternalUpdate("Restored", start, null, true, null, null);
        ev.IsCancelled.Should().BeFalse();
        ev.Title.Should().Be("Restored");
        ev.AllDay.Should().BeTrue();
    }
}
