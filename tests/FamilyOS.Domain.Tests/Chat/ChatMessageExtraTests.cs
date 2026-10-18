using FamilyOS.Domain.Chat;
using FluentAssertions;

namespace FamilyOS.Domain.Tests.Chat;

public class ChatMessageExtraTests
{
    [Fact]
    public void Create_trims_body()
    {
        var msg = ChatMessage.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "  Hello family  ");
        msg.Body.Should().Be("Hello family");
        msg.SentAtUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }
}
