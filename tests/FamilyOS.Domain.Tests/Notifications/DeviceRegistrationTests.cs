using FamilyOS.Domain.Notifications;
using FluentAssertions;

namespace FamilyOS.Domain.Tests.Notifications;

public class DeviceRegistrationTests
{
    private static readonly Guid FamilyId = Guid.NewGuid();
    private static readonly Guid MemberId = Guid.NewGuid();

    [Fact]
    public void Create_requires_token()
    {
        var act = () => DeviceRegistration.Create(FamilyId, MemberId, "  ", "ios");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_normalizes_platform()
    {
        var d = DeviceRegistration.Create(FamilyId, MemberId, "ExponentPushToken[abc]", "iOS", "Terry phone");
        d.Platform.Should().Be("ios");
        d.IsActive.Should().BeTrue();
        d.DeviceName.Should().Be("Terry phone");
    }

    [Fact]
    public void Deactivate_and_Touch()
    {
        var d = DeviceRegistration.Create(FamilyId, MemberId, "tok", "android");
        d.Deactivate();
        d.IsActive.Should().BeFalse();
        d.Touch("android", "Pixel");
        d.IsActive.Should().BeTrue();
        d.DeviceName.Should().Be("Pixel");
    }
}
