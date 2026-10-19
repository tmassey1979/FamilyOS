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

public class TaskItemLifecycleExtraTests
{
    [Fact]
    public void Decline_from_Accepted_works()
    {
        var family = Guid.NewGuid();
        var creator = Guid.NewGuid();
        var assignee = Guid.NewGuid();
        var task = TaskItem.Create(family, "Chore", creator);
        task.Assign(assignee, creator);
        task.Accept(assignee);
        // positional: (memberId, declineReasonId, note)
        task.Decline(assignee, null, "Sick");
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

    [Fact]
    public void EnableCalendarBacking_sets_flags()
    {
        var task = TaskItem.Create(Guid.NewGuid(), "Event task", Guid.NewGuid());
        var eventId = Guid.NewGuid();
        task.EnableCalendarBacking(eventId);
        task.CalendarBacked.Should().BeTrue();
        task.CalendarEventId.Should().Be(eventId);
    }

    [Fact]
    public void IsOverdue_true_when_due_in_past()
    {
        var task = TaskItem.Create(Guid.NewGuid(), "Late", Guid.NewGuid(), dueDate: DateTime.UtcNow.AddDays(-1));
        task.IsOverdue().Should().BeTrue();
    }
}
