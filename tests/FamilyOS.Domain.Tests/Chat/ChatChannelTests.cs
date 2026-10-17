using FamilyOS.Domain.Chat;
using FluentAssertions;

namespace FamilyOS.Domain.Tests.Chat;

public class ChatChannelTests
{
    [Fact]
    public void CreateFamilyChannel_defaults()
    {
        var c = ChatChannel.CreateFamilyChannel(Guid.NewGuid());
        c.Name.Should().Be("Family");
        c.Kind.Should().Be(ChatChannelKind.Family);
        c.IsActive.Should().BeTrue();
    }

    [Fact]
    public void CreateFamilyChannel_custom_name()
    {
        var c = ChatChannel.CreateFamilyChannel(Guid.NewGuid(), "  General  ");
        c.Name.Should().Be("General");
    }

    [Fact]
    public void Rename_rejects_blank()
    {
        var c = ChatChannel.CreateFamilyChannel(Guid.NewGuid());
        var act = () => c.Rename(" ");
        act.Should().Throw<ArgumentException>();
        c.Rename("Kids");
        c.Name.Should().Be("Kids");
    }

    [Fact]
    public void Deactivate()
    {
        var c = ChatChannel.CreateFamilyChannel(Guid.NewGuid());
        c.Deactivate();
        c.IsActive.Should().BeFalse();
    }
}
