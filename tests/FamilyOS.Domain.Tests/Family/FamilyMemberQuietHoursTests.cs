using FamilyOS.Domain.Common;
using FamilyOS.Domain.Family;
using FluentAssertions;

namespace FamilyOS.Domain.Tests.Family;

public class FamilyMemberQuietHoursTests
{
    [Fact]
    public void IsInQuietHours_false_when_not_configured()
    {
        var m = FamilyMember.Create(Guid.NewGuid(), Guid.NewGuid(), "Kid", FamilyRole.Child);
        m.IsInQuietHours(TimeSpan.FromHours(22)).Should().BeFalse();
    }

    [Fact]
    public void SetQuietHours_and_detect_overnight_window()
    {
        var m = FamilyMember.Create(Guid.NewGuid(), Guid.NewGuid(), "Kid", FamilyRole.Child);
        // 21:00 – 07:00 overnight
        m.SetQuietHours(TimeSpan.FromHours(21), TimeSpan.FromHours(7));

        m.IsInQuietHours(TimeSpan.FromHours(22)).Should().BeTrue();
        m.IsInQuietHours(TimeSpan.FromHours(3)).Should().BeTrue();
        m.IsInQuietHours(TimeSpan.FromHours(12)).Should().BeFalse();
    }

    [Fact]
    public void SetQuietHours_same_day_window()
    {
        var m = FamilyMember.Create(Guid.NewGuid(), Guid.NewGuid(), "Adult", FamilyRole.Adult);
        m.SetQuietHours(TimeSpan.FromHours(13), TimeSpan.FromHours(15));
        m.IsInQuietHours(TimeSpan.FromHours(14)).Should().BeTrue();
        m.IsInQuietHours(TimeSpan.FromHours(16)).Should().BeFalse();
    }

    [Fact]
    public void ClearQuietHours()
    {
        var m = FamilyMember.Create(Guid.NewGuid(), Guid.NewGuid(), "Teen", FamilyRole.Teen);
        m.SetQuietHours(TimeSpan.FromHours(22), TimeSpan.FromHours(6));
        m.ClearQuietHours();
        m.IsInQuietHours(TimeSpan.FromHours(23)).Should().BeFalse();
    }
}
