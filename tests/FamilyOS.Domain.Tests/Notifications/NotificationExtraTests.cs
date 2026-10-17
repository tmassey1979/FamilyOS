using FamilyOS.Domain.Common;
using FamilyOS.Domain.Notifications;
using FluentAssertions;

namespace FamilyOS.Domain.Tests.Notifications;

public class NotificationExtraTests
{
    [Fact]
    public void MarkActioned_also_marks_read()
    {
        var n = Notification.Create(
            Guid.NewGuid(), Guid.NewGuid(), NotificationType.TaskAssigned,
            "New task", "Please accept", Guid.NewGuid(), "Task");
        n.IsRead.Should().BeFalse();
        n.MarkActioned();
        n.IsActioned.Should().BeTrue();
        n.IsRead.Should().BeTrue();
        n.ReadAtUtc.Should().NotBeNull();
    }

    [Fact]
    public void SoftDelete_on_entity()
    {
        var n = Notification.Create(
            Guid.NewGuid(), Guid.NewGuid(), NotificationType.RequestSubmitted,
            "Req", "body");
        n.SoftDelete();
        n.IsDeleted.Should().BeTrue();
        n.ClearDomainEvents();
        n.DomainEvents.Should().BeEmpty();
    }
}
