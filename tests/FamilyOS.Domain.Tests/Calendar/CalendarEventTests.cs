using FamilyOS.Domain.Calendar;
using FluentAssertions;

namespace FamilyOS.Domain.Tests.Calendar;

public class CalendarEventTests
{
    private static readonly Guid FamilyId = Guid.NewGuid();
    private static readonly Guid MemberId = Guid.NewGuid();
    private static readonly Guid RequestId = Guid.NewGuid();

    [Fact]
    public void Create_links_request_and_sets_start()
    {
        var start = DateTime.UtcNow.AddHours(2);
        var ev = CalendarEvent.Create(
            FamilyId,
            "Ride: Soccer",
            start,
            MemberId,
            endUtc: start.AddHours(1),
            location: "School",
            linkedRequestId: RequestId);

        ev.Title.Should().Be("Ride: Soccer");
        ev.StartUtc.Should().Be(start);
        ev.Location.Should().Be("School");
        ev.LinkedRequestId.Should().Be(RequestId);
        ev.IsCancelled.Should().BeFalse();
    }

    [Fact]
    public void Cancel_marks_cancelled()
    {
        var ev = CalendarEvent.Create(FamilyId, "Dinner", DateTime.UtcNow, MemberId);
        ev.Cancel();
        ev.IsCancelled.Should().BeTrue();
    }

    [Fact]
    public void Update_changes_details()
    {
        var start = DateTime.UtcNow.AddDays(1);
        var ev = CalendarEvent.Create(FamilyId, "Old", start, MemberId);
        var newStart = start.AddHours(3);
        ev.Update("New title", newStart, newStart.AddHours(2), false, "Clinic");
        ev.Title.Should().Be("New title");
        ev.Location.Should().Be("Clinic");
        ev.StartUtc.Should().Be(newStart);
    }
}
