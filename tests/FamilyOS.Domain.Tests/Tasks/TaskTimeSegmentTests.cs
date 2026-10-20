using FamilyOS.Domain.Tasks;
using FluentAssertions;

namespace FamilyOS.Domain.Tests.Tasks;

public class TaskTimeSegmentTests
{
    [Fact]
    public void Start_and_End_compute_duration_minutes()
    {
        var taskId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        var seg = TaskTimeSegment.Start(taskId, memberId);
        seg.TaskId.Should().Be(taskId);
        seg.MemberId.Should().Be(memberId);
        seg.StartedAtUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        seg.EndedAtUtc.Should().BeNull();

        seg.End(memberId);
        seg.EndedAtUtc.Should().NotBeNull();
        seg.DurationMinutes.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public void End_wrong_member_throws()
    {
        var memberId = Guid.NewGuid();
        var seg = TaskTimeSegment.Start(Guid.NewGuid(), memberId);
        var act = () => seg.End(Guid.NewGuid());
        act.Should().Throw<UnauthorizedAccessException>();
    }

    [Fact]
    public void End_twice_throws()
    {
        var memberId = Guid.NewGuid();
        var seg = TaskTimeSegment.Start(Guid.NewGuid(), memberId);
        seg.End(memberId);
        var act = () => seg.End(memberId);
        act.Should().Throw<InvalidOperationException>();
    }
}
