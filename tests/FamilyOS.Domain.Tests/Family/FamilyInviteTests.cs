using FamilyOS.Domain.Common;
using FamilyOS.Domain.Family;
using Xunit;

namespace FamilyOS.Domain.Tests.Family;

public class FamilyInviteTests
{
    [Fact]
    public void Create_generates_open_invite_with_role()
    {
        var familyId = Guid.NewGuid();
        var creator = Guid.NewGuid();
        var invite = FamilyInvite.Create(familyId, creator, FamilyRole.Teen, emailHint: " Mia@Example.com ", validDays: 7);

        Assert.Equal(familyId, invite.FamilyId);
        Assert.Equal(creator, invite.CreatedByMemberId);
        Assert.Equal(FamilyRole.Teen, invite.Role);
        Assert.Equal("mia@example.com", invite.EmailHint);
        Assert.Equal(8, invite.Code.Length);
        Assert.True(invite.IsOpen);
        Assert.True(invite.ExpiresAtUtc > DateTime.UtcNow.AddDays(6));
        Assert.False(invite.IsRevoked);
        Assert.Null(invite.AcceptedAtUtc);
    }

    [Fact]
    public void Create_rejects_Owner_role()
    {
        Assert.Throws<ArgumentException>(() =>
            FamilyInvite.Create(Guid.NewGuid(), Guid.NewGuid(), FamilyRole.Owner));
    }

    [Fact]
    public void Accept_marks_accepted_and_closes()
    {
        var invite = FamilyInvite.Create(Guid.NewGuid(), Guid.NewGuid(), FamilyRole.Adult);
        var member = Guid.NewGuid();
        invite.Accept(member);

        Assert.False(invite.IsOpen);
        Assert.Equal(member, invite.AcceptedByMemberId);
        Assert.NotNull(invite.AcceptedAtUtc);
    }

    [Fact]
    public void Accept_when_revoked_throws()
    {
        var invite = FamilyInvite.Create(Guid.NewGuid(), Guid.NewGuid(), FamilyRole.Child);
        invite.Revoke();
        Assert.Throws<InvalidOperationException>(() => invite.Accept(Guid.NewGuid()));
    }

    [Fact]
    public void Revoke_is_idempotent_after_accept()
    {
        var invite = FamilyInvite.Create(Guid.NewGuid(), Guid.NewGuid(), FamilyRole.Adult);
        invite.Accept(Guid.NewGuid());
        invite.Revoke();
        Assert.False(invite.IsRevoked);
    }
}
