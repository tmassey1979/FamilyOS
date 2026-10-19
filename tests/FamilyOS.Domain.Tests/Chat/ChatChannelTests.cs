using FamilyOS.Domain.Chat;
using FluentAssertions;

namespace FamilyOS.Domain.Tests.Chat;

public class ChatChannelTests
{
    [Fact]
    public void Create_sets_family_and_name()
    {
        var familyId = Guid.NewGuid();
        var ch = ChatChannel.Create(familyId, "  General  ");
        ch.FamilyId.Should().Be(familyId);
        ch.Name.Should().Be("General");
        ch.IsArchived.Should().BeFalse();
    }

    [Fact]
    public void Create_rejects_empty_name()
    {
        var act = () => ChatChannel.Create(Guid.NewGuid(), "  ");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Rename_and_Archive()
    {
        var ch = ChatChannel.Create(Guid.NewGuid(), "Kids");
        ch.Rename("  Teens  ");
        ch.Name.Should().Be("Teens");
        ch.Archive();
        ch.IsArchived.Should().BeTrue();
        ch.Archive(); // idempotent
        ch.IsArchived.Should().BeTrue();
    }

    [Fact]
    public void Rename_rejects_empty()
    {
        var ch = ChatChannel.Create(Guid.NewGuid(), "X");
        var act = () => ch.Rename(" ");
        act.Should().Throw<ArgumentException>();
    }
}
