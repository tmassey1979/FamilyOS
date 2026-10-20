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
        var act = () => GoogleCalendarConnection.Create(
            FamilyId, MemberId, "a@b.com", "", "refresh", DateTime.UtcNow.AddHours(1));
        act.Should().Throw<ArgumentException>();

        var act2 = () => GoogleCalendarConnection.Create(
            FamilyId, MemberId, "a@b.com", "access", "", DateTime.UtcNow.AddHours(1));
        act2.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_and_update_tokens()
    {
        var expires = DateTime.UtcNow.AddHours(1);
        var conn = GoogleCalendarConnection.Create(
            FamilyId, MemberId, "user@gmail.com", "access-1", "refresh-1", expires);

        conn.GoogleAccountEmail.Should().Be("user@gmail.com");
        conn.IsActive.Should().BeTrue();
        conn.CalendarId.Should().Be("primary");
        conn.AccessTokenExpired().Should().BeFalse();

        var newExp = DateTime.UtcNow.AddHours(2);
        conn.UpdateTokens("access-2", newExp, "refresh-2");
        conn.AccessToken.Should().Be("access-2");
        conn.RefreshToken.Should().Be("refresh-2");
        conn.AccessTokenExpiresAtUtc.Should().Be(newExp);
    }

    [Fact]
    public void MarkSynced_SetCalendarId_Disconnect()
    {
        var conn = GoogleCalendarConnection.Create(
            FamilyId, MemberId, "u@g.com", "a", "r", DateTime.UtcNow.AddHours(1));

        conn.MarkSynced("sync-token-1");
        conn.SyncToken.Should().Be("sync-token-1");
        conn.LastSyncAtUtc.Should().NotBeNull();

        conn.SetCalendarId("secondary");
        conn.CalendarId.Should().Be("secondary");
        conn.SyncToken.Should().BeNull();

        conn.Disconnect();
        conn.IsActive.Should().BeFalse();
        conn.AccessToken.Should().BeEmpty();
        conn.RefreshToken.Should().BeEmpty();
    }

    [Fact]
    public void AccessTokenExpired_with_skew()
    {
        var conn = GoogleCalendarConnection.Create(
            FamilyId, MemberId, "u@g.com", "a", "r", DateTime.UtcNow.AddMinutes(1));
        conn.AccessTokenExpired(TimeSpan.FromMinutes(5)).Should().BeTrue();
    }
}
