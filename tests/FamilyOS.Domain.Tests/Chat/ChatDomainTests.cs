using FamilyOS.Domain.Chat;
using FluentAssertions;

namespace FamilyOS.Domain.Tests.Chat;

public class ChatDomainTests
{
    private static readonly Guid FamilyId = Guid.NewGuid();
    private static readonly Guid MemberId = Guid.NewGuid();

    [Fact]
    public void Channel_CreateFamilyChannel_Rename_Deactivate()
    {
        var ch = ChatChannel.CreateFamilyChannel(FamilyId);
        ch.FamilyId.Should().Be(FamilyId);
        ch.Name.Should().Be("Family");
        ch.Kind.Should().Be(ChatChannelKind.Family);
        ch.IsActive.Should().BeTrue();

        ch.Rename("Household");
        ch.Name.Should().Be("Household");
        ch.Deactivate();
        ch.IsActive.Should().BeFalse();
    }

    [Fact]
    public void Message_Create_requires_body()
    {
        var channelId = Guid.NewGuid();
        var msg = ChatMessage.Create(FamilyId, channelId, MemberId, "Hello family");
        msg.Body.Should().Be("Hello family");
        msg.SenderMemberId.Should().Be(MemberId);
        msg.ChannelId.Should().Be(channelId);

        var empty = () => ChatMessage.Create(FamilyId, channelId, MemberId, "  ");
        empty.Should().Throw<ArgumentException>();
    }
}
