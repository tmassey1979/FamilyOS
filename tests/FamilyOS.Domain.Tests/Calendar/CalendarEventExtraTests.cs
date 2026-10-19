using FamilyOS.Domain.Calendar;
using FluentAssertions;

namespace FamilyOS.Domain.Tests.Calendar;

public class CalendarEventExtraTests
{
    private static readonly Guid FamilyId = Guid.NewGuid();

    [Fact]
    public void UpdateDetails_changes_title_location_description()
    {
        var start = DateTime.UtcNow.AddDays(1);
        var e = CalendarEvent.Create(FamilyId, "Practice", start, start.AddHours(1));
        e.UpdateDetails("Game", "Stadium", "Bring ball");
        e.Title.Should().Be("Game");
        e.Location.Should().Be("Stadium");
        e.Description.Should().Be("Bring ball");
    }

    [Fact]
    public void LinkTask_sets_linked_task_id()
    {
        var start = DateTime.UtcNow.AddDays(1);
        var e = CalendarEvent.Create(FamilyId, "Appt", start, start.AddMinutes(30));
        var taskId = Guid.NewGuid();
        e.LinkTask(taskId);
        e.LinkedTaskId.Should().Be(taskId);
    }

    [Fact]
    public void Complete_sets_completed_flag()
    {
        var start = DateTime.UtcNow.AddDays(1);
        var e = CalendarEvent.Create(FamilyId, "Appt", start, start.AddMinutes(30));
        e.Complete();
        e.IsCompleted.Should().BeTrue();
    }
}
