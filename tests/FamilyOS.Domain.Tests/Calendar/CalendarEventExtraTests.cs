using FamilyOS.Domain.Calendar;
using FluentAssertions;

namespace FamilyOS.Domain.Tests.Calendar;

public class CalendarEventExtraTests
{
    private static readonly Guid FamilyId = Guid.NewGuid();
    private static readonly Guid Creator = Guid.NewGuid();

    [Fact]
    public void Link_task_and_request()
    {
        var taskId = Guid.NewGuid();
        var reqId = Guid.NewGuid();
        var ev = CalendarEvent.Create(
            FamilyId, "Pickup", DateTime.UtcNow, Creator,
            linkedTaskId: taskId, linkedRequestId: reqId);

        ev.LinkedTaskId.Should().Be(taskId);
        ev.LinkedRequestId.Should().Be(reqId);
    }

    [Fact]
    public void AllDay_event()
    {
        var day = DateTime.UtcNow.Date;
        var ev = CalendarEvent.Create(FamilyId, "Holiday", day, Creator, allDay: true);
        ev.AllDay.Should().BeTrue();
        ev.StartUtc.Should().Be(day);
    }
}
