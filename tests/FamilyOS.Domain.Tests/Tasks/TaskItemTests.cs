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
        task.AssigneeMemberId.Should().Be(AssigneeId);
    }

    [Fact]
    public void Accept_only_allowed_for_assignee()
    {
        var task = NewTask();
        task.Assign(AssigneeId, CreatorId);
        var act = () => task.Accept(OtherId);
        act.Should().Throw<UnauthorizedAccessException>();

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
    public void Decline_with_reason_records_history()
    {
        var task = NewTask();
        task.Assign(AssigneeId, CreatorId);
        task.Decline(AssigneeId, note: "Busy");
        task.History.Should().Contain(h => h.Detail.Contains("Busy"));
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
        task.CompletedAtUtc.Should().NotBeNull();
        task.TimeSegments.Should().NotBeEmpty();
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
    public void Start_allowed_from_Assigned_without_explicit_Accept()
    {
        var task = NewTask();
        task.Assign(AssigneeId, CreatorId);
        task.Start(AssigneeId);
        task.Status.Should().Be(FamilyTaskStatus.InProgress);
    }

    [Fact]
    public void Cannot_start_when_Created()
    {
        var task = NewTask();
        var act = () => task.Start(AssigneeId);
        act.Should().Throw<UnauthorizedAccessException>();
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
        task.AssigneeMemberId.Should().Be(OtherId);
        task.Status.Should().Be(FamilyTaskStatus.Reassigned);
        task.History.Count.Should().BeGreaterThan(1);
    }

    [Fact]
    public void Cancel_by_admin_works_from_Assigned()
    {
        var task = NewTask();
        task.Assign(AssigneeId, CreatorId);
        task.Cancel(CreatorId, "Not needed");
        task.Status.Should().Be(FamilyTaskStatus.Cancelled);
    }

    [Fact]
    public void Defer_by_assignee()
    {
        var task = NewTask();
        task.Assign(AssigneeId, CreatorId);
        var newDue = DateTime.UtcNow.AddDays(3);
        task.Defer(AssigneeId, newDue, "Tomorrow");
        task.Status.Should().Be(FamilyTaskStatus.Deferred);
        task.DueDate.Should().Be(newDue);
    }

    [Fact]
    public void Planned_times_are_independent_of_actual()
    {
        var task = NewTask();
        var start = DateTime.UtcNow.Date.AddHours(17);
        var end = start.AddMinutes(30);
        task.SetPlannedTimes(start, end, 30);
        task.PlannedStartUtc.Should().Be(start);
        task.PlannedEndUtc.Should().Be(end);
        task.EstimatedMinutes.Should().Be(30);
    }

    [Fact]
    public void IsOverdue_when_due_in_past_and_open()
    {
        var task = NewTask();
        task.SetDueDate(DateTime.UtcNow.AddDays(-1));
        task.IsOverdue().Should().BeTrue();
    }

    [Fact]
    public void IsOverdue_false_when_completed()
    {
        var task = NewTask();
        task.Assign(AssigneeId, CreatorId);
        task.Accept(AssigneeId);
        task.Start(AssigneeId);
        task.Complete(AssigneeId);
        task.SetDueDate(DateTime.UtcNow.AddDays(-2));
        task.IsOverdue().Should().BeFalse();
    }

    [Fact]
    public void IsOverdue_false_when_no_due_date()
    {
        var task = NewTask();
        task.IsOverdue().Should().BeFalse();
    }

    [Fact]
    public void UpdateDetails_and_SetPriority()
    {
        var task = NewTask();
        task.UpdateDetails(title: "Updated", category: "School");
        task.Title.Should().Be("Updated");
        task.Category.Should().Be("School");
        task.SetPriority(TaskPriority.Urgent);
        task.Priority.Should().Be(TaskPriority.Urgent);
    }

    [Fact]
    public void UpdateDetails_rejects_empty_title()
    {
        var task = NewTask();
        var act = () => task.UpdateDetails(title: "  ");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void SetDueDate_can_clear()
    {
        var task = NewTask();
        task.SetDueDate(DateTime.UtcNow.AddDays(3));
        task.DueDate.Should().NotBeNull();
        task.SetDueDate(null);
        task.DueDate.Should().BeNull();
    }

    [Fact]
    public void Skip_marks_Skipped_and_history()
    {
        var task = NewTask();
        task.Assign(AssigneeId, CreatorId);
        task.Skip(CreatorId, "Holiday");
        task.Status.Should().Be(FamilyTaskStatus.Skipped);
        task.History.Should().Contain(h => h.Status == FamilyTaskStatus.Skipped);
    }

    [Fact]
    public void Skip_rejects_already_completed()
    {
        var task = NewTask();
        task.Assign(AssigneeId, CreatorId);
        task.Accept(AssigneeId);
        task.Start(AssigneeId);
        task.Complete(AssigneeId);
        var act = () => task.Skip(CreatorId);
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Time_segments_on_start_complete()
    {
        var task = NewTask();
        task.Assign(AssigneeId, CreatorId);
        task.Accept(AssigneeId);
        task.Start(AssigneeId);
        task.TimeSegments.Should().HaveCount(1);
        task.Complete(AssigneeId);
        task.TimeSegments.First().EndedAtUtc.Should().NotBeNull();
    }
}
