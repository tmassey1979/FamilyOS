using FamilyOS.Domain.Chat;
using FluentAssertions;

namespace FamilyOS.Domain.Tests.Chat;

public class ChatChannelTests
{
    private static readonly Guid FamilyId = Guid.NewGuid();
    private static readonly Guid Creator = Guid.NewGuid();

    [Fact]
    public void Create_family_channel_defaults()
    {
        var ch = ChatChannel.Create(FamilyId, "  General  ", Creator);
        ch.FamilyId.Should().Be(FamilyId);
        ch.Name.Should().Be("General");
        ch.ChannelType.Should().Be(ChatChannelType.Family);
        ch.CreatedByMemberId.Should().Be(Creator);
        ch.IsArchived.Should().BeFalse();
        ch.DirectMemberAId.Should().BeNull();
        ch.DirectMemberBId.Should().BeNull();
    }

    [Fact]
    public void Create_rejects_empty_name()
    {
        var act = () => ChatChannel.Create(FamilyId, "  ", Creator);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_direct_normalizes_member_order()
    {
        var a = Guid.Parse("00000000-0000-0000-0000-000000000002");
        var b = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var ch = ChatChannel.Create(FamilyId, "DM", Creator, ChatChannelType.Direct, a, b);
        ch.ChannelType.Should().Be(ChatChannelType.Direct);
        // ordered by Guid comparison: b < a
        ch.DirectMemberAId.Should().Be(b);
        ch.DirectMemberBId.Should().Be(a);
    }

    [Fact]
    public void Create_direct_requires_two_distinct_members()
    {
        var m = Guid.NewGuid();
        var act1 = () => ChatChannel.Create(FamilyId, "DM", Creator, ChatChannelType.Direct, m, m);
        act1.Should().Throw<ArgumentException>();

        var act2 = () => ChatChannel.Create(FamilyId, "DM", Creator, ChatChannelType.Direct, m, null);
        act2.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Rename_and_Archive_Unarchive()
    {
        var ch = ChatChannel.Create(FamilyId, "Kids", Creator);
        ch.Rename("  Teens  ");
        ch.Name.Should().Be("Teens");

        ch.Archive();
        ch.IsArchived.Should().BeTrue();
        ch.Unarchive();
        ch.IsArchived.Should().BeFalse();
    }

    [Fact]
    public void Rename_rejects_empty()
    {
        var ch = ChatChannel.Create(FamilyId, "X", Creator);
        var act = () => ch.Rename(" ");
        act.Should().Throw<ArgumentException>();
    }
}
