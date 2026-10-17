using FamilyOS.Domain.Calendar;
using FluentAssertions;

namespace FamilyOS.Domain.Tests.Calendar;

public class CalendarEventExtraTests
{
    private static readonly Guid FamilyId = Guid.NewGuid();
    private static readonly Guid MemberId = Guid.NewGuid();

    [Fact]
    public void CreateFromExternal_sets_provider_ids()
    {
        var start = DateTime.UtcNow;
        var ev = CalendarEvent.CreateFromExternal(
            FamilyId, MemberId, "Google", "evt-123", "primary",
            "Dentist", start, start.AddHours(1), false, "Clinic", "checkup");
        ev.ExternalProvider.Should().Be("google");
        ev.ExternalEventId.Should().Be("evt-123");
        ev.ExternalCalendarId.Should().Be("primary");
        ev.Title.Should().Be("Dentist");
    }

    [Fact]
    public void CreateFromExternal_rejects_blank_provider()
    {
        var act = () => CalendarEvent.CreateFromExternal(
            FamilyId, MemberId, " ", "id", null, "T", DateTime.UtcNow, null, false, null, null);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void LinkExternal_and_ApplyExternalUpdate_uncancels()
    {
        var start = DateTime.UtcNow;
        var ev = CalendarEvent.Create(FamilyId, "Old", start, MemberId);
        ev.LinkExternal("google", "g-1", "primary");
        ev.ExternalEventId.Should().Be("g-1");
        ev.Cancel();
        ev.IsCancelled.Should().BeTrue();
        ev.ApplyExternalUpdate("New", start.AddHours(1), null, false, null, "desc");
        ev.IsCancelled.Should().BeFalse();
        ev.Title.Should().Be("New");
    }

    [Fact]
    public void LinkTask_sets_id()
    {
        var ev = CalendarEvent.Create(FamilyId, "Task", DateTime.UtcNow, MemberId);
        var taskId = Guid.NewGuid();
        ev.LinkTask(taskId);
        ev.LinkedTaskId.Should().Be(taskId);
    }
}

public class GoogleCalendarConnectionTestsExtra
{
    private static readonly Guid FamilyId = Guid.NewGuid();
    private static readonly Guid MemberId = Guid.NewGuid();

    [Fact]
    public void Create_and_token_lifecycle()
    {
        var exp = DateTime.UtcNow.AddHours(1);
        var c = GoogleCalendarConnection.Create(
            FamilyId, MemberId, "  Me@Gmail.com ", "access", "refresh", exp);
        c.GoogleAccountEmail.Should().Be("me@gmail.com");
        c.CalendarId.Should().Be("primary");
        c.AccessTokenExpired().Should().BeFalse();

        c.UpdateTokens("access2", DateTime.UtcNow.AddMinutes(-5));
        c.AccessTokenExpired().Should().BeTrue();
        c.AccessToken.Should().Be("access2");

        c.MarkSynced("sync-token");
        c.SyncToken.Should().Be("sync-token");
        c.LastSyncAtUtc.Should().NotBeNull();

        c.SetCalendarId("work");
        c.CalendarId.Should().Be("work");
        c.SyncToken.Should().BeNull();

        c.Disconnect();
        c.IsActive.Should().BeFalse();
        c.AccessToken.Should().BeEmpty();
    }

    [Fact]
    public void Create_requires_tokens()
    {
        var act = () => GoogleCalendarConnection.Create(
            FamilyId, MemberId, "a@b.com", " ", "refresh", DateTime.UtcNow);
        act.Should().Throw<ArgumentException>();
    }
}
