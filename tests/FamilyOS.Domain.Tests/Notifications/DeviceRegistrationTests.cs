using FamilyOS.Domain.Notifications;
using FluentAssertions;

namespace FamilyOS.Domain.Tests.Notifications;

public class DeviceRegistrationTests
{
    [Fact]
    public void Register_sets_token_and_platform()
    {
        var familyId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        var d = DeviceRegistration.Register(familyId, memberId, "ExponentPushToken[xxx]", "ios");
        d.FamilyId.Should().Be(familyId);
        d.MemberId.Should().Be(memberId);
        d.ExpoPushToken.Should().Be("ExponentPushToken[xxx]");
        d.Platform.Should().Be("ios");
        d.IsActive.Should().BeTrue();
        d.LastSeenAtUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Register_rejects_empty_token()
    {
        var act = () => DeviceRegistration.Register(Guid.NewGuid(), Guid.NewGuid(), "  ", null);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Touch_updates_last_seen()
    {
        var d = DeviceRegistration.Register(Guid.NewGuid(), Guid.NewGuid(), "token-1", "android");
        var before = d.LastSeenAtUtc;
        Thread.Sleep(5);
        d.Touch();
        d.LastSeenAtUtc.Should().BeOnOrAfter(before);
    }

    [Fact]
    public void Deactivate_sets_inactive()
    {
        var d = DeviceRegistration.Register(Guid.NewGuid(), Guid.NewGuid(), "token-1", "ios");
        d.Deactivate();
        d.IsActive.Should().BeFalse();
    }
}
