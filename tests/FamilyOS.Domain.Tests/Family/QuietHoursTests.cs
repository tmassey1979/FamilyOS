using FamilyOS.Domain.Family;
using Xunit;

namespace FamilyOS.Domain.Tests.Family;

public class QuietHoursTests
{
    private static FamilyMember Member() =>
        FamilyMember.Create(Guid.NewGuid(), Guid.NewGuid(), FamilyRole.Adult, "Terry");

    [Fact]
    public void Disabled_IsNeverInQuietHours()
    {
        var m = Member();
        m.SetQuietHours(false, null, null);
        Assert.False(m.IsInQuietHours(TimeSpan.FromHours(23)));
    }

    [Fact]
    public void SameDayWindow_Works()
    {
        var m = Member();
        m.SetQuietHours(true, 22 * 60, 23 * 60); // 22:00–23:00
        Assert.True(m.IsInQuietHours(TimeSpan.FromHours(22.5)));
        Assert.False(m.IsInQuietHours(TimeSpan.FromHours(21)));
        Assert.False(m.IsInQuietHours(TimeSpan.FromHours(23.5)));
    }

    [Fact]
    public void OvernightWindow_Works()
    {
        var m = Member();
        m.SetQuietHours(true, 22 * 60, 7 * 60); // 22:00–07:00
        Assert.True(m.IsInQuietHours(TimeSpan.FromHours(23)));
        Assert.True(m.IsInQuietHours(TimeSpan.FromHours(2)));
        Assert.False(m.IsInQuietHours(TimeSpan.FromHours(12)));
        Assert.False(m.IsInQuietHours(TimeSpan.FromHours(8)));
    }

    [Fact]
    public void InvalidMinutes_Throws()
    {
        var m = Member();
        Assert.Throws<ArgumentException>(() => m.SetQuietHours(true, -1, 60));
        Assert.Throws<ArgumentException>(() => m.SetQuietHours(true, null, 60));
    }
}
