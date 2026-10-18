using FamilyOS.Domain.Chat;
using FluentAssertions;

namespace FamilyOS.Domain.Tests.Chat;

public class ChatMessageExtraTests
{
    [Fact]
    public void Create_trims_body_and_rejects_empty()
    {
        var msg = ChatMessage.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "  Hello family  ");
        msg.Body.Should().Be("Hello family");

        var act = () => ChatMessage.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "  ");
        act.Should().Throw<ArgumentException>();
    }
}
