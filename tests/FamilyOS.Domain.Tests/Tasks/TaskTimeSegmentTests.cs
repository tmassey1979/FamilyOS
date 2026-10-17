using FamilyOS.Domain.Tasks;
using FluentAssertions;

namespace FamilyOS.Domain.Tests.Tasks;

public class TaskTimeSegmentTests
{
    [Fact]
    public void Start_and_End()
    {
        var taskId = Guid.NewGuid();
        var member = Guid.NewGuid();
        var seg = TaskTimeSegment.Start(taskId, member);
        seg.EndedAtUtc.Should().BeNull();
        seg.End(member);
        seg.EndedAtUtc.Should().NotBeNull();
        seg.DurationMinutes.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public void End_twice_throws()
    {
        var member = Guid.NewGuid();
        var seg = TaskTimeSegment.Start(Guid.NewGuid(), member);
        seg.End(member);
        var act = () => seg.End(member);
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void End_by_other_member_throws()
    {
        var seg = TaskTimeSegment.Start(Guid.NewGuid(), Guid.NewGuid());
        var act = () => seg.End(Guid.NewGuid());
        act.Should().Throw<UnauthorizedAccessException>();
    }
}
