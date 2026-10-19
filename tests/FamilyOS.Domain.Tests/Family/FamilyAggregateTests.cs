using FamilyOS.Domain.Common;
using FamilyOS.Domain.Family;
using FluentAssertions;

namespace FamilyOS.Domain.Tests.Family;

public class FamilyAggregateTests
{
    [Fact]
    public void Family_Create_UpdateName_Timezone()
    {
        var f = Domain.Family.Family.Create("Henderson");
        f.Name.Should().Be("Henderson");
        f.UpdateName("Henderson Family");
        f.Name.Should().Be("Henderson Family");
        f.SetTimezone("America/Chicago");
        f.Timezone.Should().Be("America/Chicago");
    }

    [Fact]
    public void FamilyMember_Create_quiet_hours_and_roles()
    {
        var familyId = Guid.NewGuid();
        var m = FamilyMember.Create(familyId, "Alex", FamilyRole.Teen);

        m.DisplayName.Should().Be("Alex");
        m.Role.Should().Be(FamilyRole.Teen);
        m.IsActive.Should().BeTrue();

        m.UpdateQuietHours(startMinute: 21 * 60, endMinute: 7 * 60);
        m.QuietHoursStartMinute.Should().Be(21 * 60);
        m.QuietHoursEndMinute.Should().Be(7 * 60);

        // 23:00 → in overnight quiet hours
        m.IsInQuietHours(TimeSpan.FromHours(23)).Should().BeTrue();
        // 10:00 → not
        m.IsInQuietHours(TimeSpan.FromHours(10)).Should().BeFalse();

        m.ChangeRole(FamilyRole.Adult);
        m.Role.Should().Be(FamilyRole.Adult);

        m.UpdateDisplayName("Alex H");
        m.DisplayName.Should().Be("Alex H");

        var userId = Guid.NewGuid();
        m.LinkUser(userId);
        m.UserId.Should().Be(userId);

        m.Deactivate();
        m.IsActive.Should().BeFalse();
    }

    [Fact]
    public void FamilyInvite_Create_Accept_Revoke()
    {
        var familyId = Guid.NewGuid();
        var inviter = Guid.NewGuid();
        var invite = FamilyInvite.Create(familyId, inviter, FamilyRole.Adult, email: "new@example.com");

        invite.Token.Should().NotBeNullOrWhiteSpace();
        invite.Status.Should().Be(InviteStatus.Pending);
        invite.IsUsable().Should().BeTrue();

        invite.Accept(acceptedByUserId: Guid.NewGuid());
        invite.Status.Should().Be(InviteStatus.Accepted);
        invite.AcceptedAtUtc.Should().NotBeNull();
        invite.IsUsable().Should().BeFalse();
    }

    [Fact]
    public void FamilyInvite_Revoke_blocks_accept()
    {
        var invite = FamilyInvite.Create(Guid.NewGuid(), Guid.NewGuid(), FamilyRole.Teen);
        invite.Revoke();
        invite.Status.Should().Be(InviteStatus.Revoked);
        var act = () => invite.Accept(Guid.NewGuid());
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void FamilyInvite_expired_not_usable()
    {
        var invite = FamilyInvite.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            FamilyRole.Child,
            expiresAtUtc: DateTime.UtcNow.AddDays(-1));
        invite.IsUsable(DateTime.UtcNow).Should().BeFalse();
    }
}
