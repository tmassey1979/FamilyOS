using FamilyOS.Domain.Notifications;
using FluentAssertions;
using Xunit;

namespace FamilyOS.Domain.Tests.Notifications;

public class NotificationTests
{
    private static readonly Guid FamilyId = Guid.NewGuid();
    private static readonly Guid MemberId = Guid.NewGuid();
    private static readonly Guid EntityId = Guid.NewGuid();

    [Fact]
    public void Create_sets_fields_and_unread()
    {
        var n = Notification.Create(
            FamilyId, MemberId, NotificationType.RequestSubmitted,
            " New request ", "  Needs approval  ", EntityId, "Request");

        n.FamilyId.Should().Be(FamilyId);
        n.RecipientMemberId.Should().Be(MemberId);
        n.Title.Should().Be("New request");
        n.Body.Should().Be("Needs approval");
        n.RelatedEntityId.Should().Be(EntityId);
        n.RelatedEntityType.Should().Be("Request");
        n.IsRead.Should().BeFalse();
        n.IsActioned.Should().BeFalse();
        n.ReadAtUtc.Should().BeNull();
    }

    [Fact]
    public void MarkRead_sets_flag_and_time()
    {
        var n = Notification.Create(FamilyId, MemberId, NotificationType.TaskAssigned, "T", "B");
        n.MarkRead();
        n.IsRead.Should().BeTrue();
        n.ReadAtUtc.Should().NotBeNull();
    }

    [Fact]
    public void MarkActioned_also_marks_read()
    {
        var n = Notification.Create(FamilyId, MemberId, NotificationType.RequestApproved, "T", "B");
        n.MarkActioned();
        n.IsActioned.Should().BeTrue();
        n.IsRead.Should().BeTrue();
        n.ReadAtUtc.Should().NotBeNull();
    }
}
