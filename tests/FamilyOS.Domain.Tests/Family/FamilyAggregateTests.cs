using FamilyOS.Domain.Common;
using FamilyOS.Domain.Family;
using FluentAssertions;

namespace FamilyOS.Domain.Tests.Family;

public class FamilyAggregateTests
{
    [Fact]
    public void Family_Create_UpdateName()
    {
        var f = Domain.Family.Family.Create("Henderson");
        f.Name.Should().Be("Henderson");
        f.UpdateName("Henderson Family");
        f.Name.Should().Be("Henderson Family");
    }

    [Fact]
    public void FamilyMember_Create_quiet_hours_and_roles()
    {
        var familyId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var m = FamilyMember.Create(familyId, userId, FamilyRole.Teen, "Alex");

        m.DisplayName.Should().Be("Alex");
        m.Role.Should().Be(FamilyRole.Teen);
        m.UserId.Should().Be(userId);
        m.IsActive.Should().BeTrue();
        m.CanApproveRequests.Should().BeFalse();

        m.SetQuietHours(enabled: true, startMinutes: 21 * 60, endMinutes: 7 * 60);
        m.QuietHoursEnabled.Should().BeTrue();
        m.QuietHoursStartMinutes.Should().Be(21 * 60);

        m.IsInQuietHours(TimeSpan.FromHours(23)).Should().BeTrue();
        m.IsInQuietHours(TimeSpan.FromHours(10)).Should().BeFalse();

        m.ChangeRole(FamilyRole.Adult);
        m.CanApproveRequests.Should().BeTrue();

        m.UpdateDisplayName("Alex H");
        m.DisplayName.Should().Be("Alex H");

        m.Deactivate();
        m.IsActive.Should().BeFalse();
    }

    [Fact]
    public void FamilyInvite_Create_Accept_Revoke()
    {
        var familyId = Guid.NewGuid();
        var inviter = Guid.NewGuid();
        var invite = FamilyInvite.Create(familyId, inviter, FamilyRole.Adult, emailHint: "new@example.com");

        invite.Code.Should().NotBeNullOrWhiteSpace();
        invite.IsOpen.Should().BeTrue();

        invite.Accept(memberId: Guid.NewGuid());
        invite.AcceptedAtUtc.Should().NotBeNull();
        invite.IsOpen.Should().BeFalse();
    }

    [Fact]
    public void FamilyInvite_Revoke_blocks_accept()
    {
        var invite = FamilyInvite.Create(Guid.NewGuid(), Guid.NewGuid(), FamilyRole.Teen);
        invite.Revoke();
        invite.IsRevoked.Should().BeTrue();
        var act = () => invite.Accept(Guid.NewGuid());
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void FamilyInvite_cannot_invite_as_Owner()
    {
        var act = () => FamilyInvite.Create(Guid.NewGuid(), Guid.NewGuid(), FamilyRole.Owner);
        act.Should().Throw<ArgumentException>();
    }
}
