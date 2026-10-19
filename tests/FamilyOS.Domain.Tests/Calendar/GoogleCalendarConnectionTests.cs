using FamilyOS.Domain.Calendar;
using FluentAssertions;

namespace FamilyOS.Domain.Tests.Calendar;

public class GoogleCalendarConnectionTests
{
    [Fact]
    public void Connect_sets_tokens_and_email()
    {
        var familyId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        var expires = DateTime.UtcNow.AddHours(1);
        var c = GoogleCalendarConnection.Connect(
            familyId,
            memberId,
            googleAccountEmail: " Parent@Example.com ",
            accessToken: "access",
            refreshToken: "refresh",
            accessTokenExpiresAtUtc: expires);

        c.FamilyId.Should().Be(familyId);
        c.MemberId.Should().Be(memberId);
        c.GoogleAccountEmail.Should().Be("parent@example.com");
        c.AccessToken.Should().Be("access");
        c.RefreshToken.Should().Be("refresh");
        c.AccessTokenExpiresAtUtc.Should().Be(expires);
        c.NeedsRefresh().Should().BeFalse();
    }

    [Fact]
    public void Connect_rejects_missing_fields()
    {
        var exp = DateTime.UtcNow.AddHours(1);
        var act = () => GoogleCalendarConnection.Connect(
            Guid.NewGuid(), Guid.NewGuid(), "", "a", "r", exp);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void NeedsRefresh_true_when_near_expiry()
    {
        var c = GoogleCalendarConnection.Connect(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "user@x.com",
            "a",
            "r",
            DateTime.UtcNow.AddMinutes(1)); // within 2-minute margin
        c.NeedsRefresh().Should().BeTrue();
    }

    [Fact]
    public void RefreshAccessToken_and_MarkSynced()
    {
        var c = GoogleCalendarConnection.Connect(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "user@x.com",
            "old",
            "r",
            DateTime.UtcNow.AddMinutes(-5));

        var newExp = DateTime.UtcNow.AddHours(2);
        c.RefreshAccessToken("new-access", newExp);
        c.AccessToken.Should().Be("new-access");
        c.AccessTokenExpiresAtUtc.Should().Be(newExp);
        c.NeedsRefresh().Should().BeFalse();

        c.MarkSynced();
        c.LastSyncAtUtc.Should().NotBeNull();
        c.LastSyncAtUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void UpdateSelectedCalendar()
    {
        var c = GoogleCalendarConnection.Connect(
            Guid.NewGuid(), Guid.NewGuid(), "u@x.com", "a", "r", DateTime.UtcNow.AddHours(1));
        c.UpdateSelectedCalendar("cal-123");
        c.SelectedCalendarId.Should().Be("cal-123");
    }
}
