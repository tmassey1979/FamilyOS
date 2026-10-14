using FamilyOS.Domain.Calendar;
using FluentAssertions;

namespace FamilyOS.Domain.Tests.Calendar;

public class GoogleCalendarConnectionTests
{
    private static readonly Guid FamilyId = Guid.NewGuid();
    private static readonly Guid MemberId = Guid.NewGuid();

    [Fact]
    public void Create_requires_tokens()
    {
        var act = () => GoogleCalendarConnection.Create(FamilyId, MemberId, "a@b.com", "", "refresh", DateTime.UtcNow.AddHours(1));
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void AccessTokenExpired_respects_skew()
    {
        var conn = GoogleCalendarConnection.Create(
            FamilyId, MemberId, "terry@gmail.com", "at", "rt",
            DateTime.UtcNow.AddMinutes(1));
        conn.AccessTokenExpired(TimeSpan.FromMinutes(2)).Should().BeTrue();
        conn.AccessTokenExpired(TimeSpan.FromSeconds(1)).Should().BeFalse();
    }

    [Fact]
    public void Disconnect_clears_tokens()
    {
        var conn = GoogleCalendarConnection.Create(
            FamilyId, MemberId, "terry@gmail.com", "at", "rt",
            DateTime.UtcNow.AddHours(1));
        conn.Disconnect();
        conn.IsActive.Should().BeFalse();
        conn.AccessToken.Should().BeEmpty();
        conn.RefreshToken.Should().BeEmpty();
    }

    [Fact]
    public void CalendarEvent_CreateFromExternal_links_provider()
    {
        var start = DateTime.UtcNow.AddHours(3);
        var ev = CalendarEvent.CreateFromExternal(
            FamilyId, MemberId, "google", "gcal-123", "primary",
            "Dentist", start, start.AddHours(1), false, "Clinic", "checkup");
        ev.ExternalProvider.Should().Be("google");
        ev.ExternalEventId.Should().Be("gcal-123");
        ev.Title.Should().Be("Dentist");
    }
}
