using FamilyOS.Domain.Chat;
using FluentAssertions;

namespace FamilyOS.Domain.Tests.Chat;

public class ChatDomainTests
{
    private static readonly Guid FamilyId = Guid.NewGuid();
    private static readonly Guid MemberId = Guid.NewGuid();

    [Fact]
    public void Channel_CreateGeneral_and_Archive_Rename()
    {
        var ch = ChatChannel.CreateGeneral(FamilyId);
        ch.FamilyId.Should().Be(FamilyId);
        ch.Name.Should().Be("General");
        ch.Kind.Should().Be(ChatChannelKind.General);
        ch.IsArchived.Should().BeFalse();

        ch.Rename("Family Chat");
        ch.Name.Should().Be("Family Chat");
        ch.Archive();
        ch.IsArchived.Should().BeTrue();
    }

    [Fact]
    public void Channel_CreateDm()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var ch = ChatChannel.CreateDm(FamilyId, a, b);
        ch.Kind.Should().Be(ChatChannelKind.Direct);
        ch.MemberAId.Should().Be(a);
        ch.MemberBId.Should().Be(b);
    }

    [Fact]
    public void Message_Create_and_SoftDelete()
    {
        var channelId = Guid.NewGuid();
        var msg = ChatMessage.Create(FamilyId, channelId, MemberId, "Hello family");
        msg.Body.Should().Be("Hello family");
        msg.IsDeleted.Should().BeFalse();
        msg.SenderMemberId.Should().Be(MemberId);

        msg.SoftDelete();
        msg.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public void Message_rejects_empty_body()
    {
        var act = () => ChatMessage.Create(FamilyId, Guid.NewGuid(), MemberId, "  ");
        act.Should().Throw<ArgumentException>();
    }
}
