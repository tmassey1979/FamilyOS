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
        task.Decline(assignee);
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
    public void IsOverdue_true_when_due_in_past()
    {
        var task = TaskItem.Create(
            Guid.NewGuid(),
            "Late",
            Guid.NewGuid(),
            dueDate: DateTime.UtcNow.AddDays(-1));
        task.IsOverdue().Should().BeTrue();
    }

    [Fact]
    public void Pause_and_Resume_round_trip()
    {
        var family = Guid.NewGuid();
        var creator = Guid.NewGuid();
        var assignee = Guid.NewGuid();
        var task = TaskItem.Create(family, "Focus", creator);
        task.Assign(assignee, creator);
        task.Accept(assignee);
        task.Start(assignee);
        task.Status.Should().Be(FamilyTaskStatus.InProgress);
        task.Pause(assignee);
        task.Status.Should().Be(FamilyTaskStatus.Paused);
        task.Resume(assignee);
        task.Status.Should().Be(FamilyTaskStatus.InProgress);
    }
}
