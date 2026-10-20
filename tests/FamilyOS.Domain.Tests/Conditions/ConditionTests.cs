using FamilyOS.Domain.Conditions;
using FluentAssertions;

namespace FamilyOS.Domain.Tests.Conditions;

public class ConditionTests
{
    private static readonly Guid FamilyId = Guid.NewGuid();
    private static readonly Guid MemberId = Guid.NewGuid();
    private static readonly Guid RequestId = Guid.NewGuid();

    [Fact]
    public void Create_sets_pending_manual()
    {
        var c = Condition.Create(FamilyId, "Finish homework", ConditionType.Manual, requestId: RequestId);
        c.FamilyId.Should().Be(FamilyId);
        c.Name.Should().Be("Finish homework");
        c.Type.Should().Be(ConditionType.Manual);
        c.Status.Should().Be(ConditionStatus.Pending);
        c.RequestId.Should().Be(RequestId);
        c.SatisfiedByMemberId.Should().BeNull();
    }

    [Fact]
    public void Satisfy_from_pending()
    {
        var c = Condition.Create(FamilyId, "Confirm", ConditionType.Manual);
        c.Satisfy(MemberId);
        c.Status.Should().Be(ConditionStatus.Satisfied);
        c.SatisfiedByMemberId.Should().Be(MemberId);
        c.SatisfiedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public void Satisfy_twice_throws()
    {
        var c = Condition.Create(FamilyId, "Confirm", ConditionType.Manual);
        c.Satisfy(MemberId);
        var act = () => c.Satisfy(MemberId);
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Fail_sets_failed()
    {
        var c = Condition.Create(FamilyId, "Confirm", ConditionType.Approval);
        c.Fail("denied");
        c.Status.Should().Be(ConditionStatus.Failed);
        c.Description.Should().Be("denied");
    }

    [Fact]
    public void Expire_only_from_pending()
    {
        var c = Condition.Create(FamilyId, "Time box", ConditionType.TimeWindow);
        c.Expire();
        c.Status.Should().Be(ConditionStatus.Expired);

        var done = Condition.Create(FamilyId, "Done", ConditionType.Manual);
        done.Satisfy(MemberId);
        done.Expire();
        done.Status.Should().Be(ConditionStatus.Satisfied);
    }

    [Fact]
    public void Cancel_always_works()
    {
        var c = Condition.Create(FamilyId, "X", ConditionType.Custom);
        c.Cancel();
        c.Status.Should().Be(ConditionStatus.Cancelled);
    }

    [Fact]
    public void TaskCompletion_type_supported()
    {
        var taskId = Guid.NewGuid();
        var c = Condition.Create(FamilyId, "Complete chore", ConditionType.TaskCompletion, taskId: taskId);
        c.Type.Should().Be(ConditionType.TaskCompletion);
        c.TaskId.Should().Be(taskId);
    }
}
