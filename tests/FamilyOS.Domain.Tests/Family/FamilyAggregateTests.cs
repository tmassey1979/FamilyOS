using FamilyOS.Domain.Common;
using FamilyOS.Domain.Family;
using FluentAssertions;
using FamilyEntity = FamilyOS.Domain.Family.Family;

namespace FamilyOS.Domain.Tests.Family;

public class FamilyAggregateTests
{
    [Fact]
    public void Create_trims_name_and_seeds_decline_reasons()
    {
        var f = FamilyEntity.Create("  Henderson  ");
        f.Name.Should().Be("Henderson");
        f.TimeZone.Should().Be("America/Chicago");
        f.Currency.Should().Be("USD");
        f.DeclineReasons.Should().HaveCount(5);
        f.DeclineReasons.Should().Contain(r => r.Text == "Other");
    }

    [Fact]
    public void Create_rejects_empty_name()
    {
        var act = () => FamilyEntity.Create("  ");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void AddMember_links_user()
    {
        var f = FamilyEntity.Create("Smith");
        var user = User.Create("kc-1", "a@b.com", "Ann", "Smith");
        var member = f.AddMember(user, FamilyRole.Owner, "Ann");
        f.Members.Should().ContainSingle();
        member.Role.Should().Be(FamilyRole.Owner);
        member.DisplayName.Should().Be("Ann");
        member.UserId.Should().Be(user.Id);
    }

    [Fact]
    public void AddMember_rejects_duplicate_user()
    {
        var f = FamilyEntity.Create("Smith");
        var user = User.Create("kc-1", "a@b.com", "Ann", "Smith");
        f.AddMember(user, FamilyRole.Owner, "Ann");
        var act = () => f.AddMember(user, FamilyRole.Adult, "Ann again");
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void UpdateName_trims()
    {
        var f = FamilyEntity.Create("Old");
        f.UpdateName("  New Name  ");
        f.Name.Should().Be("New Name");
    }

    [Fact]
    public void AddDeclineReason_appends()
    {
        var f = FamilyEntity.Create("X");
        var before = f.DeclineReasons.Count;
        f.AddDeclineReason("Busy", 99);
        f.DeclineReasons.Should().HaveCount(before + 1);
        f.DeclineReasons.Should().Contain(r => r.Text == "Busy" && r.SortOrder == 99);
    }
}

public class UserTests
{
    [Fact]
    public void Create_normalizes_email()
    {
        var u = User.Create("sub-1", "  Ada@Example.COM ", "Ada", "Lovelace");
        u.Email.Should().Be("ada@example.com");
        u.ExternalIdentityId.Should().Be("sub-1");
        u.DisplayName.Should().Be("Ada Lovelace");
    }

    [Fact]
    public void Create_rejects_missing_identity_or_email()
    {
        var a = () => User.Create(" ", "a@b.com", "A", "B");
        a.Should().Throw<ArgumentException>();
        var b = () => User.Create("id", "  ", "A", "B");
        b.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void DisplayName_falls_back_to_email()
    {
        var u = User.Create("id", "solo@x.com", "", "");
        u.DisplayName.Should().Be("solo@x.com");
    }

    [Fact]
    public void AssignToFamily_sets_ids()
    {
        var u = User.Create("id", "a@b.com", "A", "B");
        var familyId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        u.AssignToFamily(familyId, memberId);
        u.FamilyId.Should().Be(familyId);
        u.FamilyMemberId.Should().Be(memberId);
    }

    [Fact]
    public void AssignToFamily_rejects_other_family()
    {
        var u = User.Create("id", "a@b.com", "A", "B");
        u.AssignToFamily(Guid.NewGuid(), Guid.NewGuid());
        var act = () => u.AssignToFamily(Guid.NewGuid(), Guid.NewGuid());
        act.Should().Throw<InvalidOperationException>();
    }
}

public class DeclineReasonTests
{
    [Fact]
    public void Create_trims_text()
    {
        var r = DeclineReason.Create(Guid.NewGuid(), "  Too busy  ", 1);
        r.Text.Should().Be("Too busy");
        r.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public void Create_rejects_empty()
    {
        var act = () => DeclineReason.Create(Guid.NewGuid(), " ", 1);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Disable_and_Enable()
    {
        var r = DeclineReason.Create(Guid.NewGuid(), "Other", 5);
        r.Disable();
        r.IsEnabled.Should().BeFalse();
        r.Enable();
        r.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public void Update_changes_text_and_order()
    {
        var r = DeclineReason.Create(Guid.NewGuid(), "A", 1);
        r.Update("B", 2);
        r.Text.Should().Be("B");
        r.SortOrder.Should().Be(2);
    }
}

public class FamilyMemberRoleTests
{
    private static FamilyMember Member(FamilyRole role) =>
        FamilyMember.Create(Guid.NewGuid(), Guid.NewGuid(), role, "Person");

    [Fact]
    public void Owner_capabilities()
    {
        var m = Member(FamilyRole.Owner);
        m.CanManageFamily.Should().BeTrue();
        m.CanApproveRequests.Should().BeTrue();
        m.CanAssignTasks.Should().BeTrue();
        m.CanOverridePolicies.Should().BeTrue();
    }

    [Fact]
    public void Adult_can_approve_but_not_manage()
    {
        var m = Member(FamilyRole.Adult);
        m.CanApproveRequests.Should().BeTrue();
        m.CanAssignTasks.Should().BeTrue();
        m.CanManageFamily.Should().BeFalse();
        m.CanOverridePolicies.Should().BeFalse();
    }

    [Fact]
    public void Child_has_no_admin_powers()
    {
        var m = Member(FamilyRole.Child);
        m.CanApproveRequests.Should().BeFalse();
        m.CanAssignTasks.Should().BeFalse();
        m.CanManageFamily.Should().BeFalse();
    }

    [Fact]
    public void ChangeRole_and_Deactivate()
    {
        var m = Member(FamilyRole.Teen);
        m.ChangeRole(FamilyRole.Adult);
        m.Role.Should().Be(FamilyRole.Adult);
        m.Deactivate();
        m.IsActive.Should().BeFalse();
    }

    [Fact]
    public void UpdateDisplayName_rejects_blank()
    {
        var m = Member(FamilyRole.Adult);
        var act = () => m.UpdateDisplayName("  ");
        act.Should().Throw<ArgumentException>();
        m.UpdateDisplayName(" New ");
        m.DisplayName.Should().Be("New");
    }

    [Fact]
    public void FullDayQuiet_when_start_equals_end()
    {
        var m = Member(FamilyRole.Adult);
        m.SetQuietHours(true, 0, 0);
        m.IsInQuietHours(TimeSpan.FromHours(12)).Should().BeTrue();
    }
}
