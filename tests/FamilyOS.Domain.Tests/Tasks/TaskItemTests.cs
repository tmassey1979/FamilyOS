using FamilyOS.Domain.Common;
using FamilyOS.Domain.Tasks;
using FluentAssertions;

namespace FamilyOS.Domain.Tests.Tasks;

public class TaskItemTests
{
    private static readonly Guid FamilyId = Guid.NewGuid();
    private static readonly Guid CreatorId = Guid.NewGuid();
    private static readonly Guid AssigneeId = Guid.NewGuid();
    private static readonly Guid OtherId = Guid.NewGuid();

    private static TaskItem NewTask(string title = "Take out trash") =>
        TaskItem.Create(FamilyId, title, CreatorId, category: "Chores");

    [Fact]
    public void Create_sets_Created_status_and_history()
    {
        var task = NewTask();
        task.Status.Should().Be(FamilyTaskStatus.Created);
        task.Title.Should().Be("Take out trash");
        task.History.Should().ContainSingle(h => h.Status == FamilyTaskStatus.Created);
    }

    [Fact]
    public void Create_rejects_empty_title()
    {
        var act = () => TaskItem.Create(FamilyId, "  ", CreatorId);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Assign_moves_to_Assigned()
    {
        var task = NewTask();
        task.Assign(AssigneeId, CreatorId);
        task.Status.Should().Be(FamilyTaskStatus.Assigned);
        task.AssignedToMemberId.Should().Be(AssigneeId);
    }

    [Fact]
    public void Accept_only_allowed_for_assignee()
    {
        var task = NewTask();
        task.Assign(AssigneeId, CreatorId);
        var wrong = () => task.Accept(OtherId);
        wrong.Should().Throw<UnauthorizedAccessException>();
        task.Accept(AssigneeId);
        task.Status.Should().Be(FamilyTaskStatus.Accepted);
    }

    [Fact]
    public void Assignment_does_not_equal_acceptance()
    {
        var task = NewTask();
        task.Assign(AssigneeId, CreatorId);
        task.Status.Should().Be(FamilyTaskStatus.Assigned);
        task.Status.Should().NotBe(FamilyTaskStatus.Accepted);
    }

    [Fact]
    public void Decline_does_not_require_reason()
    {
        var task = NewTask();
        task.Assign(AssigneeId, CreatorId);
        task.Decline(AssigneeId);
        task.Status.Should().Be(FamilyTaskStatus.Declined);
    }

    [Fact]
    public void Full_lifecycle_Create_Assign_Accept_Start_Complete()
    {
        var task = NewTask();
        task.Assign(AssigneeId, CreatorId);
        task.Accept(AssigneeId);
        task.Start(AssigneeId);
        task.Status.Should().Be(FamilyTaskStatus.InProgress);
        task.Complete(AssigneeId);
        task.Status.Should().Be(FamilyTaskStatus.Completed);
        task.TimeSegments.Should().OnlyContain(s => s.EndedAtUtc.HasValue);
    }

    [Fact]
    public void Pause_and_Resume()
    {
        var task = NewTask();
        task.Assign(AssigneeId, CreatorId);
        task.Accept(AssigneeId);
        task.Start(AssigneeId);
        task.Pause(AssigneeId);
        task.Status.Should().Be(FamilyTaskStatus.Paused);
        task.Resume(AssigneeId);
        task.Status.Should().Be(FamilyTaskStatus.InProgress);
    }

    [Fact]
    public void Cannot_start_before_accept()
    {
        var task = NewTask();
        task.Assign(AssigneeId, CreatorId);
        var act = () => task.Start(AssigneeId);
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Cannot_assign_completed_task()
    {
        var task = NewTask();
        task.Assign(AssigneeId, CreatorId);
        task.Accept(AssigneeId);
        task.Start(AssigneeId);
        task.Complete(AssigneeId);
        var act = () => task.Assign(OtherId, CreatorId);
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Reassign_preserves_history_and_sets_Assigned()
    {
        var task = NewTask();
        task.Assign(AssigneeId, CreatorId);
        task.Reassign(OtherId, CreatorId);
        task.AssignedToMemberId.Should().Be(OtherId);
        task.Status.Should().Be(FamilyTaskStatus.Assigned);
        task.History.Should().Contain(h => h.Status == FamilyTaskStatus.Reassigned);
    }

    [Fact]
    public void Cancel_by_admin_works_from_Assigned()
    {
        var task = NewTask();
        task.Assign(AssigneeId, CreatorId);
        task.Cancel(CreatorId, "No longer needed");
        task.Status.Should().Be(FamilyTaskStatus.Cancelled);
    }

    [Fact]
    public void Defer_by_assignee()
    {
        var task = NewTask();
        task.Assign(AssigneeId, CreatorId);
        task.Accept(AssigneeId);
        task.Defer(AssigneeId, "Tomorrow");
        task.Status.Should().Be(FamilyTaskStatus.Deferred);
    }

    [Fact]
    public void Planned_times_are_independent_of_actual()
    {
        var task = NewTask();
        task.SetPlannedTimes(new TimeOnly(17, 0), new TimeOnly(17, 30), 30);
        task.Assign(AssigneeId, CreatorId);
        task.Accept(AssigneeId);
        task.Start(AssigneeId);
        task.Complete(AssigneeId);
        task.PlannedStartTime.Should().Be(new TimeOnly(17, 0));
        task.EstimatedDurationMinutes.Should().Be(30);
        task.PlannedEndTime.Should().Be(new TimeOnly(17, 30));
    }
}
