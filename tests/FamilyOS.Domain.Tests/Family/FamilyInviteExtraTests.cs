using FamilyOS.Domain.Common;
using FamilyOS.Domain.Family;
using FluentAssertions;

namespace FamilyOS.Domain.Tests.Family;

public class FamilyInviteExtraTests
{
    private static readonly Guid FamilyId = Guid.NewGuid();
    private static readonly Guid OwnerId = Guid.NewGuid();

    [Fact]
    public void Create_sets_token_role_and_pending()
    {
        var invite = FamilyInvite.Create(FamilyId, OwnerId, FamilyRole.Teen, daysValid: 7, email: " teen@x.com ");
        invite.FamilyId.Should().Be(FamilyId);
        invite.InvitedByMemberId.Should().Be(OwnerId);
        invite.Role.Should().Be(FamilyRole.Teen);
        invite.Email.Should().Be("teen@x.com");
        invite.Token.Should().NotBeNullOrWhiteSpace();
        invite.Status.Should().Be(FamilyInviteStatus.Pending);
        invite.IsExpired().Should().BeFalse();
    }

    [Fact]
    public void Accept_marks_accepted()
    {
        var invite = FamilyInvite.Create(FamilyId, OwnerId, FamilyRole.Adult);
        invite.Accept();
        invite.Status.Should().Be(FamilyInviteStatus.Accepted);
        invite.AcceptedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public void Accept_twice_is_idempotent()
    {
        var invite = FamilyInvite.Create(FamilyId, OwnerId, FamilyRole.Child);
        invite.Accept();
        invite.Accept(); // no throw
        invite.Status.Should().Be(FamilyInviteStatus.Accepted);
    }

    [Fact]
    public void Revoke_from_pending()
    {
        var invite = FamilyInvite.Create(FamilyId, OwnerId, FamilyRole.Adult);
        invite.Revoke();
        invite.Status.Should().Be(FamilyInviteStatus.Revoked);
    }

    [Fact]
    public void Accept_after_revoke_throws()
    {
        var invite = FamilyInvite.Create(FamilyId, OwnerId, FamilyRole.Adult);
        invite.Revoke();
        var act = () => invite.Accept();
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Create_rejects_non_positive_days()
    {
        var act = () => FamilyInvite.Create(FamilyId, OwnerId, FamilyRole.Adult, daysValid: 0);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
