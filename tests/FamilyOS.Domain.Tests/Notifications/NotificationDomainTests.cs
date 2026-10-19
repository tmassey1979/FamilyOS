using FamilyOS.Domain.Common;
using FamilyOS.Domain.Notifications;
using FluentAssertions;

namespace FamilyOS.Domain.Tests.Notifications;

public class NotificationDomainTests
{
    private static readonly Guid FamilyId = Guid.NewGuid();
    private static readonly Guid MemberId = Guid.NewGuid();

    [Fact]
    public void Create_unread_then_MarkRead_and_Actioned()
    {
        var n = Notification.Create(
            FamilyId,
            MemberId,
            NotificationType.TaskAssigned,
            "Task assigned",
            "You have a new chore",
            relatedEntityId: Guid.NewGuid(),
            relatedEntityType: "Task");

        n.IsRead.Should().BeFalse();
        n.Title.Should().Be("Task assigned");
        n.RelatedEntityType.Should().Be("Task");

        n.MarkRead();
        n.IsRead.Should().BeTrue();
        n.ReadAtUtc.Should().NotBeNull();

        n.MarkActioned();
        n.IsActioned.Should().BeTrue();
    }

    [Fact]
    public void DeviceRegistration_create_touch_deactivate()
    {
        var d = DeviceRegistration.Create(
            FamilyId,
            MemberId,
            "ExponentPushToken[xxx]",
            platform: "android",
            deviceName: "Pixel");

        d.Token.Should().StartWith("ExponentPushToken");
        d.Platform.Should().Be("android");
        d.IsActive.Should().BeTrue();

        d.Touch(platform: "android", deviceName: "Pixel 8");
        d.DeviceName.Should().Be("Pixel 8");
        d.LastSeenAtUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));

        d.Deactivate();
        d.IsActive.Should().BeFalse();
    }
}
