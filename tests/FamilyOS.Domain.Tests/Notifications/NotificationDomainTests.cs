using FamilyOS.Domain.Notifications;
using FluentAssertions;

namespace FamilyOS.Domain.Tests.Notifications;

public class NotificationDomainTests
{
    private static readonly Guid FamilyId = Guid.NewGuid();
    private static readonly Guid MemberId = Guid.NewGuid();

    [Fact]
    public void Create_unread_then_MarkRead()
    {
        var n = Notification.Create(
            FamilyId,
            MemberId,
            "Task assigned",
            "You have a new chore",
            relatedEntityType: "Task",
            relatedEntityId: Guid.NewGuid());

        n.IsRead.Should().BeFalse();
        n.Title.Should().Be("Task assigned");
        n.RelatedEntityType.Should().Be("Task");

        n.MarkRead();
        n.IsRead.Should().BeTrue();
        n.ReadAtUtc.Should().NotBeNull();
    }

    [Fact]
    public void DeviceRegistration_create_and_touch()
    {
        var d = DeviceRegistration.Create(
            FamilyId,
            MemberId,
            "ExponentPushToken[xxx]",
            platform: "android",
            deviceName: "Pixel");

        d.PushToken.Should().StartWith("ExponentPushToken");
        d.Platform.Should().Be("android");
        d.IsActive.Should().BeTrue();

        var before = d.LastSeenAtUtc;
        d.Touch();
        d.LastSeenAtUtc.Should().BeOnOrAfter(before ?? DateTime.MinValue);
    }

    [Fact]
    public void DeviceRegistration_Deactivate()
    {
        var d = DeviceRegistration.Create(FamilyId, MemberId, "token-1", "ios");
        d.Deactivate();
        d.IsActive.Should().BeFalse();
    }
}
