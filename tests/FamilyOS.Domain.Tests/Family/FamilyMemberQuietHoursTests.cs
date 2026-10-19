using FamilyOS.Domain.Common;
using FamilyOS.Domain.Family;
using FluentAssertions;

namespace FamilyOS.Domain.Tests.Family;

public class FamilyMemberQuietHoursTests
{
    [Fact]
    public void IsInQuietHours_false_when_not_enabled()
    {
        var m = FamilyMember.Create(Guid.NewGuid(), "Kid", FamilyRole.Child);
        m.IsInQuietHours(TimeSpan.FromHours(22)).Should().BeFalse();
    }

    [Fact]
    public void SetQuietHours_overnight_window()
    {
        var m = FamilyMember.Create(Guid.NewGuid(), "Kid", FamilyRole.Child);
        // 21:00 – 07:00 overnight (minutes from midnight)
        m.SetQuietHours(21 * 60, 7 * 60);
        m.QuietHoursEnabled.Should().BeTrue();

        m.IsInQuietHours(TimeSpan.FromHours(22)).Should().BeTrue();
        m.IsInQuietHours(TimeSpan.FromHours(3)).Should().BeTrue();
        m.IsInQuietHours(TimeSpan.FromHours(12)).Should().BeFalse();
    }

    [Fact]
    public void SetQuietHours_same_day_window()
    {
        var m = FamilyMember.Create(Guid.NewGuid(), "Adult", FamilyRole.Adult);
        m.SetQuietHours(13 * 60, 15 * 60);
        m.IsInQuietHours(TimeSpan.FromHours(14)).Should().BeTrue();
        m.IsInQuietHours(TimeSpan.FromHours(16)).Should().BeFalse();
    }

    [Fact]
    public void ClearQuietHours_disables()
    {
        var m = FamilyMember.Create(Guid.NewGuid(), "Teen", FamilyRole.Teen);
        m.SetQuietHours(22 * 60, 6 * 60);
        m.SetQuietHours(null, null);
        m.QuietHoursEnabled.Should().BeFalse();
        m.IsInQuietHours(TimeSpan.FromHours(23)).Should().BeFalse();
    }
}
