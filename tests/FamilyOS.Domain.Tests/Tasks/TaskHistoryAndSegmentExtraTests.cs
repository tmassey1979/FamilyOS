using FamilyOS.Domain.Common;
using FamilyOS.Domain.Tasks;
using FluentAssertions;

namespace FamilyOS.Domain.Tests.Tasks;

public class TaskHistoryEntryTests
{
    [Fact]
    public void Create_records_status_and_detail()
    {
        var taskId = Guid.NewGuid();
        var actor = Guid.NewGuid();
        var h = TaskHistoryEntry.Create(taskId, FamilyTaskStatus.Assigned, actor, "Assigned to kid");
        h.TaskId.Should().Be(taskId);
        h.Status.Should().Be(FamilyTaskStatus.Assigned);
        h.ActorMemberId.Should().Be(actor);
        h.Detail.Should().Be("Assigned to kid");
        h.TimestampUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }
}

public class TaskItemLinkCalendarTests
{
    [Fact]
    public void LinkCalendarEvent_sets_backed()
    {
        var task = TaskItem.Create(Guid.NewGuid(), "Event task", Guid.NewGuid());
        var calId = Guid.NewGuid();
        task.LinkCalendarEvent(calId);
        task.CalendarEventId.Should().Be(calId);
        task.CalendarBacked.Should().BeTrue();
    }

    [Fact]
    public void Decline_from_Accepted_works()
    {
        var family = Guid.NewGuid();
        var creator = Guid.NewGuid();
        var assignee = Guid.NewGuid();
        var task = TaskItem.Create(family, "Chore", creator);
        task.Assign(assignee, creator);
        task.Accept(assignee);
        task.Decline(assignee, note: "Sick");
        task.Status.Should().Be(FamilyTaskStatus.Declined);
    }

    [Fact]
    public void Complete_from_Accepted_without_start()
    {
        var family = Guid.NewGuid();
        var creator = Guid.NewGuid();
        var assignee = Guid.NewGuid();
        var task = TaskItem.Create(family, "Quick", creator);
        task.Assign(assignee, creator);
        task.Accept(assignee);
        task.Complete(assignee);
        task.Status.Should().Be(FamilyTaskStatus.Completed);
    }
}
