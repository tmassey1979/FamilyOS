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
            "Task assigned",
            "You have a new chore",
            relatedType: "Task",
            relatedId: related);

        n.FamilyId.Should().Be(familyId);
        n.MemberId.Should().Be(memberId);
        n.Title.Should().Be("Task assigned");
        n.Body.Should().Be("You have a new chore");
        n.RelatedEntityType.Should().Be("Task");
        n.RelatedEntityId.Should().Be(related);
        n.IsRead.Should().BeFalse();
        n.CreatedUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void MarkRead_sets_flag_and_timestamp()
    {
        var n = Notification.Create(Guid.NewGuid(), Guid.NewGuid(), "Hi", null);
        n.MarkRead();
        n.IsRead.Should().BeTrue();
        n.ReadAtUtc.Should().NotBeNull();
        n.MarkRead();
        n.IsRead.Should().BeTrue();
    }
}
