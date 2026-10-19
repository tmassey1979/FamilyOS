using FamilyOS.Domain.Common;
using FamilyOS.Domain.Notifications;
using FluentAssertions;

namespace FamilyOS.Domain.Tests.Notifications;

public class NotificationTests
{
    [Fact]
    public void Create_sets_fields_and_unread()
    {
        var familyId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        var related = Guid.NewGuid();
        var n = Notification.Create(
            familyId,
            memberId,
            NotificationType.TaskAssigned,
            "Task assigned",
            "You have a new chore",
            relatedEntityId: related,
            relatedEntityType: "Task");

        n.FamilyId.Should().Be(familyId);
        n.RecipientMemberId.Should().Be(memberId);
        n.Type.Should().Be(NotificationType.TaskAssigned);
        n.Title.Should().Be("Task assigned");
        n.Body.Should().Be("You have a new chore");
        n.RelatedEntityType.Should().Be("Task");
        n.RelatedEntityId.Should().Be(related);
        n.IsRead.Should().BeFalse();
        n.CreatedAtUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void MarkRead_and_MarkActioned()
    {
        var n = Notification.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            NotificationType.Info,
            "Hi",
            "Body");
        n.MarkRead();
        n.IsRead.Should().BeTrue();
        n.ReadAtUtc.Should().NotBeNull();

        n.MarkActioned();
        n.IsActioned.Should().BeTrue();
    }
}
