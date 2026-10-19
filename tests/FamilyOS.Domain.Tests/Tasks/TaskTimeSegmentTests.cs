using FamilyOS.Domain.Tasks;
using FluentAssertions;

namespace FamilyOS.Domain.Tests.Tasks;

public class TaskTimeSegmentTests
{
    [Fact]
    public void Start_and_End_compute_duration()
    {
        var taskId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        var seg = TaskTimeSegment.Start(taskId, memberId);
        seg.TaskId.Should().Be(taskId);
        seg.MemberId.Should().Be(memberId);
        seg.StartedAtUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        seg.EndedAtUtc.Should().BeNull();
        seg.Duration.Should().BeNull();

        seg.End(memberId);
        seg.EndedAtUtc.Should().NotBeNull();
        seg.Duration.Should().NotBeNull();
        seg.Duration!.Value.Should().BeGreaterThanOrEqualTo(TimeSpan.Zero);
    }

    [Fact]
    public void End_is_idempotent()
    {
        var seg = TaskTimeSegment.Start(Guid.NewGuid(), Guid.NewGuid());
        seg.End(Guid.NewGuid());
        var ended = seg.EndedAtUtc;
        seg.End(Guid.NewGuid());
        seg.EndedAtUtc.Should().Be(ended);
    }
}
