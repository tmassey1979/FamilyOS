using FamilyOS.Domain.Calendar;
using FluentAssertions;

namespace FamilyOS.Domain.Tests.Calendar;

public class GoogleCalendarConnectionTests
{
    [Fact]
    public void Create_sets_tokens_and_expiry()
    {
        var familyId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        var expires = DateTime.UtcNow.AddHours(1);
        var c = GoogleCalendarConnection.Create(
            familyId,
            memberId,
            accessToken: "access",
            refreshToken: "refresh",
            accessTokenExpiresAtUtc: expires,
            calendarId: "primary",
            accountEmail: " parent@example.com ");

        c.FamilyId.Should().Be(familyId);
        c.MemberId.Should().Be(memberId);
        c.AccessToken.Should().Be("access");
        c.RefreshToken.Should().Be("refresh");
        c.AccessTokenExpiresAtUtc.Should().Be(expires);
        c.CalendarId.Should().Be("primary");
        c.AccountEmail.Should().Be("parent@example.com");
        c.IsAccessTokenExpired().Should().BeFalse();
    }

    [Fact]
    public void IsAccessTokenExpired_true_when_past_margin()
    {
        var c = GoogleCalendarConnection.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "a",
            "r",
            DateTime.UtcNow.AddMinutes(-10));
        c.IsAccessTokenExpired().Should().BeTrue();
    }

    [Fact]
    public void UpdateTokens_and_MarkSynced()
    {
        var c = GoogleCalendarConnection.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "old",
            "r",
            DateTime.UtcNow.AddMinutes(-5));

        var newExp = DateTime.UtcNow.AddHours(2);
        c.UpdateTokens("new-access", "new-refresh", newExp);
        c.AccessToken.Should().Be("new-access");
        c.RefreshToken.Should().Be("new-refresh");
        c.AccessTokenExpiresAtUtc.Should().Be(newExp);
        c.IsAccessTokenExpired().Should().BeFalse();

        c.MarkSynced();
        c.LastSyncedAtUtc.Should().NotBeNull();
        c.LastSyncedAtUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void SetCalendarId()
    {
        var c = GoogleCalendarConnection.Create(
            Guid.NewGuid(), Guid.NewGuid(), "a", "r", DateTime.UtcNow.AddHours(1));
        c.SetCalendarId("  cal-123  ");
        c.CalendarId.Should().Be("cal-123");
    }
}
