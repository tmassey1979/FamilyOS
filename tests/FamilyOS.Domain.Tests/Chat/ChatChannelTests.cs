using FamilyOS.Domain.Chat;
using FluentAssertions;

namespace FamilyOS.Domain.Tests.Chat;

public class ChatChannelTests
{
    private static readonly Guid FamilyId = Guid.NewGuid();

    [Fact]
    public void CreateFamilyChannel_defaults()
    {
        var ch = ChatChannel.CreateFamilyChannel(FamilyId);
        ch.FamilyId.Should().Be(FamilyId);
        ch.Name.Should().Be("Family");
        ch.Kind.Should().Be(ChatChannelKind.Family);
        ch.IsActive.Should().BeTrue();
    }

    [Fact]
    public void CreateFamilyChannel_custom_name()
    {
        var ch = ChatChannel.CreateFamilyChannel(FamilyId, "House Chat");
        ch.Name.Should().Be("House Chat");
    }

    [Fact]
    public void Rename_and_Deactivate()
    {
        var ch = ChatChannel.CreateFamilyChannel(FamilyId);
        ch.Rename("Parents");
        ch.Name.Should().Be("Parents");

        var act = () => ch.Rename("  ");
        act.Should().Throw<ArgumentException>();

        ch.Deactivate();
        ch.IsActive.Should().BeFalse();
    }
}
