using FamilyOS.Domain.Common;
using FamilyOS.Domain.Family;
using FluentAssertions;

namespace FamilyOS.Domain.Tests.Family;

public class FamilyMemberQuietHoursTests
{
    private static FamilyMember Member(FamilyRole role = FamilyRole.Child) =>
        FamilyMember.Create(Guid.NewGuid(), Guid.NewGuid(), role, "Kid");

    [Fact]
    public void IsInQuietHours_false_when_not_enabled()
    {
        var m = Member();
        m.IsInQuietHours(TimeSpan.FromHours(22)).Should().BeFalse();
    }

    [Fact]
    public void SetQuietHours_overnight_window()
    {
        var m = Member();
        m.SetQuietHours(true, 21 * 60, 7 * 60);
        m.QuietHoursEnabled.Should().BeTrue();
        m.IsInQuietHours(TimeSpan.FromHours(22)).Should().BeTrue();
        m.IsInQuietHours(TimeSpan.FromHours(3)).Should().BeTrue();
        m.IsInQuietHours(TimeSpan.FromHours(12)).Should().BeFalse();
    }

    [Fact]
    public void SetQuietHours_same_day_window()
    {
        var m = Member(FamilyRole.Adult);
        m.SetQuietHours(true, 13 * 60, 15 * 60);
        m.IsInQuietHours(TimeSpan.FromHours(14)).Should().BeTrue();
        m.IsInQuietHours(TimeSpan.FromHours(16)).Should().BeFalse();
    }

    [Fact]
    public void Disable_quiet_hours()
    {
        var m = Member(FamilyRole.Teen);
        m.SetQuietHours(true, 22 * 60, 6 * 60);
        m.SetQuietHours(false, null, null);
        m.QuietHoursEnabled.Should().BeFalse();
        m.IsInQuietHours(TimeSpan.FromHours(23)).Should().BeFalse();
    }
}
