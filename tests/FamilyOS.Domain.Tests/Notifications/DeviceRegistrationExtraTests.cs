using FamilyOS.Domain.Notifications;
using FluentAssertions;

namespace FamilyOS.Domain.Tests.Notifications;

public class DeviceRegistrationExtraTests
{
    [Fact]
    public void Create_normalizes_platform()
    {
        var d = DeviceRegistration.Create(
            Guid.NewGuid(), Guid.NewGuid(), " ExponentPushToken[xxx] ", "  IOS ", "  iPhone  ");
        d.Token.Should().Be("ExponentPushToken[xxx]");
        d.Platform.Should().Be("ios");
        d.DeviceName.Should().Be("iPhone");
        d.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Create_rejects_empty_token()
    {
        var act = () => DeviceRegistration.Create(Guid.NewGuid(), Guid.NewGuid(), "  ", "android");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Touch_reactivates_and_updates()
    {
        var d = DeviceRegistration.Create(Guid.NewGuid(), Guid.NewGuid(), "t1", "android");
        d.Deactivate();
        d.IsActive.Should().BeFalse();
        d.Touch("ios", "Pad");
        d.IsActive.Should().BeTrue();
        d.Platform.Should().Be("ios");
        d.DeviceName.Should().Be("Pad");
    }
}
