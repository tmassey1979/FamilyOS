using FamilyOS.Domain.Common;
using FamilyOS.Domain.Tasks;
using FluentAssertions;

namespace FamilyOS.Domain.Tests.Tasks;

public class TaskItemControlFlowTests
{
    private static readonly Guid FamilyId = Guid.NewGuid();
    private static readonly Guid Creator = Guid.NewGuid();
    private static readonly Guid Assignee = Guid.NewGuid();

    private static TaskItem AcceptedTask()
    {
        var t = TaskItem.Create(FamilyId, "Chore", Creator, category: "home");
        t.Assign(Assignee, Creator);
        t.Accept(Assignee);
        return t;
    }

    private static TaskItem AssignedTask()
    {
        var t = TaskItem.Create(FamilyId, "Chore", Creator, category: "home");
        t.Assign(Assignee, Creator);
        return t;
    }

    [Fact]
    public void Pause_and_Resume()
    {
        var t = AcceptedTask();
        t.Start(Assignee);
        // Domain keeps InProgress while paused; ends the open segment
        t.Pause(Assignee);
        t.Status.Should().Be(FamilyTaskStatus.InProgress);
        t.TimeSegments.Should().OnlyContain(s => s.EndedAtUtc.HasValue);
        t.Resume(Assignee);
        t.Status.Should().Be(FamilyTaskStatus.InProgress);
        t.TimeSegments.Should().Contain(s => !s.EndedAtUtc.HasValue);
    }

    [Fact]
    public void SetPriority_and_UpdateDetails()
    {
        var t = TaskItem.Create(FamilyId, "Old", Creator);
        t.SetPriority(TaskPriority.High);
        t.Priority.Should().Be(TaskPriority.High);
        t.UpdateDetails("New title", "desc", "chores");
        t.Title.Should().Be("New title");
        t.Description.Should().Be("desc");
        t.Category.Should().Be("chores");
    }

    [Fact]
    public void SetDueDate_and_PlannedTimes()
    {
        var t = TaskItem.Create(FamilyId, "Timed", Creator);
        var due = DateTime.UtcNow.AddDays(2);
        t.SetDueDate(due);
        t.DueDateUtc.Should().Be(due);

        var start = DateTime.UtcNow.AddHours(1);
        var end = start.AddHours(2);
        t.SetPlannedTimes(start, end, estimatedMinutes: 120);
        t.PlannedStartUtc.Should().Be(start);
        t.PlannedEndUtc.Should().Be(end);
        t.EstimatedMinutes.Should().Be(120);
    }

    [Fact]
    public void Defer_from_Assigned()
    {
        // Defer only allowed from Assigned (not Accepted)
        var t = AssignedTask();
        var until = DateTime.UtcNow.AddDays(3);
        t.Defer(Assignee, until, "Busy");
        t.Status.Should().Be(FamilyTaskStatus.Deferred);
        t.DueDateUtc.Should().Be(until);
    }

    [Fact]
    public void Reassign_from_Accepted()
    {
        var t = AcceptedTask();
        var other = Guid.NewGuid();
        t.Reassign(other, Creator);
        t.AssigneeMemberId.Should().Be(other);
        t.Status.Should().Be(FamilyTaskStatus.Assigned);
    }

    [Fact]
    public void Skip_from_Assigned()
    {
        var t = AssignedTask();
        t.Skip(Creator, "Not needed");
        t.Status.Should().Be(FamilyTaskStatus.Skipped);
    }

    [Fact]
    public void Cancel_from_Created()
    {
        var t = TaskItem.Create(FamilyId, "Cancel me", Creator);
        t.Cancel(Creator, "Changed mind");
        t.Status.Should().Be(FamilyTaskStatus.Cancelled);
    }
}
