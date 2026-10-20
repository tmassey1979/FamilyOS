using FamilyOS.Domain.Common;
using FamilyOS.Domain.Family;
using FluentAssertions;

namespace FamilyOS.Domain.Tests.Family;

public class FamilyInviteTests
{
    private static readonly Guid FamilyId = Guid.NewGuid();
    private static readonly Guid Creator = Guid.NewGuid();
    private static readonly Guid Acceptor = Guid.NewGuid();

    [Fact]
    public void Create_generates_code_and_role()
    {
        var inv = FamilyInvite.Create(FamilyId, Creator, FamilyRole.Adult, "kid@example.com", validDays: 7);
        inv.FamilyId.Should().Be(FamilyId);
        inv.CreatedByMemberId.Should().Be(Creator);
        inv.Role.Should().Be(FamilyRole.Adult);
        inv.EmailHint.Should().Be("kid@example.com");
        inv.Code.Should().HaveLength(8);
        inv.IsOpen.Should().BeTrue();
        inv.IsRevoked.Should().BeFalse();
        inv.AcceptedAtUtc.Should().BeNull();
        inv.ExpiresAtUtc.Should().BeAfter(DateTime.UtcNow.AddDays(6));
    }

    [Fact]
    public void Cannot_invite_as_Owner()
    {
        var act = () => FamilyInvite.Create(FamilyId, Creator, FamilyRole.Owner);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Accept_closes_invite()
    {
        var inv = FamilyInvite.Create(FamilyId, Creator, FamilyRole.Teen);
        inv.Accept(Acceptor);
        inv.IsOpen.Should().BeFalse();
        inv.AcceptedByMemberId.Should().Be(Acceptor);
        inv.AcceptedAtUtc.Should().NotBeNull();

        var act = () => inv.Accept(Guid.NewGuid());
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Revoke_blocks_accept()
    {
        var inv = FamilyInvite.Create(FamilyId, Creator, FamilyRole.Child);
        inv.Revoke();
        inv.IsRevoked.Should().BeTrue();
        inv.IsOpen.Should().BeFalse();
        var act = () => inv.Accept(Acceptor);
        act.Should().Throw<InvalidOperationException>();
    }
}
