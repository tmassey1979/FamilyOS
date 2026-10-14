using FamilyOS.Domain.Chat;
using FluentAssertions;

namespace FamilyOS.Domain.Tests.Chat;

public class ChatMessageTests
{
    private static readonly Guid FamilyId = Guid.NewGuid();
    private static readonly Guid ChannelId = Guid.NewGuid();
    private static readonly Guid MemberId = Guid.NewGuid();

    [Fact]
    public void Create_requires_body()
    {
        var act = () => ChatMessage.Create(FamilyId, ChannelId, MemberId, "  ");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_trims_and_sets_sender()
    {
        var m = ChatMessage.Create(FamilyId, ChannelId, MemberId, "  Hello family  ");
        m.Body.Should().Be("Hello family");
        m.SenderMemberId.Should().Be(MemberId);
        m.IsDeleted.Should().BeFalse();
    }

    [Fact]
    public void SoftDelete_marks_deleted()
    {
        var m = ChatMessage.Create(FamilyId, ChannelId, MemberId, "bye");
        m.SoftDelete();
        m.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public void Family_channel_defaults()
    {
        var c = ChatChannel.CreateFamilyChannel(FamilyId);
        c.Kind.Should().Be(ChatChannelKind.Family);
        c.Name.Should().Be("Family");
        c.IsActive.Should().BeTrue();
    }
}
