using FamilyOS.Domain.Common;
using FamilyOS.Domain.Family;
using FluentAssertions;

namespace FamilyOS.Domain.Tests.Family;

public class FamilyAggregateTests
{
    [Fact]
    public void Family_Create_and_Rename()
    {
        var f = Domain.Family.Family.Create("Henderson", createdByUserId: Guid.NewGuid());
        f.Name.Should().Be("Henderson");
        f.Rename("Henderson Family");
        f.Name.Should().Be("Henderson Family");
    }

    [Fact]
    public void FamilyMember_Create_with_role_and_quiet_hours()
    {
        var familyId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var m = FamilyMember.Create(familyId, userId, "Alex", FamilyRole.Teen);

        m.DisplayName.Should().Be("Alex");
        m.Role.Should().Be(FamilyRole.Teen);
        m.IsActive.Should().BeTrue();

        m.SetQuietHours(enabled: true, startHour: 21, endHour: 7);
        m.QuietHoursEnabled.Should().BeTrue();
        m.QuietHoursStartHour.Should().Be(21);
        m.QuietHoursEndHour.Should().Be(7);
    }

    [Fact]
    public void FamilyMember_IsInQuietHours_overnight_window()
    {
        var m = FamilyMember.Create(Guid.NewGuid(), Guid.NewGuid(), "Kid", FamilyRole.Child);
        m.SetQuietHours(true, 22, 7);

        // 23:00 local-as-utc → in quiet hours
        var late = new DateTime(2026, 10, 19, 23, 0, 0, DateTimeKind.Utc);
        m.IsInQuietHours(late).Should().BeTrue();

        // 10:00 → not in quiet hours
        var morning = new DateTime(2026, 10, 19, 10, 0, 0, DateTimeKind.Utc);
        m.IsInQuietHours(morning).Should().BeFalse();
    }

    [Fact]
    public void FamilyMember_Deactivate()
    {
        var m = FamilyMember.Create(Guid.NewGuid(), Guid.NewGuid(), "Gone", FamilyRole.Adult);
        m.Deactivate();
        m.IsActive.Should().BeFalse();
    }

    [Fact]
    public void FamilyInvite_Create_Accept_Revoke()
    {
        var familyId = Guid.NewGuid();
        var inviter = Guid.NewGuid();
        var invite = FamilyInvite.Create(familyId, inviter, FamilyRole.Adult, email: "new@example.com");

        invite.Code.Should().NotBeNullOrWhiteSpace();
        invite.IsAccepted.Should().BeFalse();
        invite.IsRevoked.Should().BeFalse();

        invite.Accept(acceptedByUserId: Guid.NewGuid());
        invite.IsAccepted.Should().BeTrue();
        invite.AcceptedAtUtc.Should().NotBeNull();
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
}
