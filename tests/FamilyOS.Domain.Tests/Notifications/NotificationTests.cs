using FamilyOS.Domain.Notifications;
using Xunit;

namespace FamilyOS.Domain.Tests.Notifications;

public class NotificationTests
{
    private static readonly Guid FamilyId = Guid.NewGuid();
    private static readonly Guid MemberId = Guid.NewGuid();
    private static readonly Guid EntityId = Guid.NewGuid();

    [Fact]
    public void Create_sets_fields_and_unread()
    {
        var n = Notification.Create(
            FamilyId, MemberId, NotificationType.RequestSubmitted,
            " New request ", "  Needs approval  ", EntityId, "Request");

        Assert.Equal(FamilyId, n.FamilyId);
        Assert.Equal(MemberId, n.RecipientMemberId);
        Assert.Equal("New request", n.Title);
        Assert.Equal("Needs approval", n.Body);
        Assert.Equal(EntityId, n.RelatedEntityId);
        Assert.Equal("Request", n.RelatedEntityType);
        Assert.False(n.IsRead);
        Assert.False(n.IsActioned);
        Assert.Null(n.ReadAtUtc);
    }

    [Fact]
    public void MarkRead_sets_flag_and_time()
    {
        var n = Notification.Create(FamilyId, MemberId, NotificationType.TaskAssigned, "T", "B");
        n.MarkRead();
        Assert.True(n.IsRead);
        Assert.NotNull(n.ReadAtUtc);
    }

    [Fact]
    public void MarkActioned_also_marks_read()
    {
        var n = Notification.Create(FamilyId, MemberId, NotificationType.RequestApproved, "T", "B");
        n.MarkActioned();
        Assert.True(n.IsActioned);
        Assert.True(n.IsRead);
        Assert.NotNull(n.ReadAtUtc);
    }
}
