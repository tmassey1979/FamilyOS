using FamilyOS.Domain.Common;
using FamilyOS.Domain.Family;
using FluentAssertions;

namespace FamilyOS.Domain.Tests.Family;

public class FamilyInviteExtraTests
{
    private static readonly Guid FamilyId = Guid.NewGuid();
    private static readonly Guid OwnerId = Guid.NewGuid();

    [Fact]
    public void Create_sets_token_and_role()
    {
        var invite = FamilyInvite.Create(FamilyId, OwnerId, FamilyRole.Teen, expiresInDays: 7);
        invite.FamilyId.Should().Be(FamilyId);
        invite.CreatedByMemberId.Should().Be(OwnerId);
        invite.Role.Should().Be(FamilyRole.Teen);
        invite.Token.Should().NotBeNullOrWhiteSpace();
        invite.IsAccepted.Should().BeFalse();
        invite.IsExpired.Should().BeFalse();
        invite.IsUsable.Should().BeTrue();
    }

    [Fact]
    public void Accept_marks_accepted()
    {
        var invite = FamilyInvite.Create(FamilyId, OwnerId, FamilyRole.Adult);
        var memberId = Guid.NewGuid();
        invite.Accept(memberId);
        invite.IsAccepted.Should().BeTrue();
        invite.AcceptedByMemberId.Should().Be(memberId);
        invite.IsUsable.Should().BeFalse();
    }

    [Fact]
    public void Accept_twice_throws()
    {
        var invite = FamilyInvite.Create(FamilyId, OwnerId, FamilyRole.Child);
        invite.Accept(Guid.NewGuid());
        var act = () => invite.Accept(Guid.NewGuid());
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Revoke_makes_unusable()
    {
        var invite = FamilyInvite.Create(FamilyId, OwnerId, FamilyRole.Adult);
        invite.Revoke();
        invite.IsRevoked.Should().BeTrue();
        invite.IsUsable.Should().BeFalse();
    }
}
