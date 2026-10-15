using FamilyOS.Domain.Common;
using FamilyOS.Domain.Family;

namespace FamilyOS.Domain.Tests.Family;

public class FamilyInviteTests
{
    [Fact]
    public void Create_Generates_Code_And_Rejects_Owner_Role()
    {
        var invite = FamilyInvite.Create(Guid.NewGuid(), Guid.NewGuid(), FamilyRole.Adult);
        Assert.False(string.IsNullOrWhiteSpace(invite.Code));
        Assert.Equal(8, invite.Code.Length);
        Assert.True(invite.IsOpen);

        Assert.Throws<ArgumentException>(() =>
            FamilyInvite.Create(Guid.NewGuid(), Guid.NewGuid(), FamilyRole.Owner));
    }

    [Fact]
    public void Accept_Closes_Invite()
    {
        var invite = FamilyInvite.Create(Guid.NewGuid(), Guid.NewGuid(), FamilyRole.Teen);
        var memberId = Guid.NewGuid();
        invite.Accept(memberId);
        Assert.False(invite.IsOpen);
        Assert.Equal(memberId, invite.AcceptedByMemberId);
        Assert.Throws<InvalidOperationException>(() => invite.Accept(Guid.NewGuid()));
    }
}
