using FamilyOS.Domain.Chat;
using FluentAssertions;

namespace FamilyOS.Domain.Tests.Chat;

public class ChatDomainTests
{
    private static readonly Guid FamilyId = Guid.NewGuid();
    private static readonly Guid MemberId = Guid.NewGuid();

    [Fact]
    public void Channel_Create_sets_name_and_family()
    {
        var ch = ChatChannel.Create(FamilyId, "General", MemberId);
        ch.FamilyId.Should().Be(FamilyId);
        ch.Name.Should().Be("General");
        ch.CreatedByMemberId.Should().Be(MemberId);
        ch.IsArchived.Should().BeFalse();
    }

    [Fact]
    public void Channel_Archive_and_Rename()
    {
        var ch = ChatChannel.Create(FamilyId, "Kids", MemberId);
        ch.Rename("Teens");
        ch.Name.Should().Be("Teens");
        ch.Archive();
        ch.IsArchived.Should().BeTrue();
    }

    [Fact]
    public void Message_Create_and_Edit_and_SoftDelete()
    {
        var channelId = Guid.NewGuid();
        var msg = ChatMessage.Create(channelId, FamilyId, MemberId, "Hello family");
        msg.Body.Should().Be("Hello family");
        msg.IsDeleted.Should().BeFalse();

        msg.Edit("Hello everyone");
        msg.Body.Should().Be("Hello everyone");
        msg.EditedAtUtc.Should().NotBeNull();

        msg.SoftDelete();
        msg.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public void Message_rejects_empty_body()
    {
        var act = () => ChatMessage.Create(Guid.NewGuid(), FamilyId, MemberId, "  ");
        act.Should().Throw<ArgumentException>();
    }
}
