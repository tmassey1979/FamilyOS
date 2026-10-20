using FamilyOS.Domain.Common;
using FamilyOS.Domain.Family;
using FluentAssertions;

namespace FamilyOS.Domain.Tests.Family;

public class UserAndFamilyTests
{
    [Fact]
    public void User_Create_normalizes_email_and_names()
    {
        var u = User.Create("kc-123", "  Alice@Example.COM ", "  Alice ", " Smith ");
        u.ExternalIdentityId.Should().Be("kc-123");
        u.Email.Should().Be("alice@example.com");
        u.FirstName.Should().Be("Alice");
        u.LastName.Should().Be("Smith");
        u.DisplayName.Should().Be("Alice Smith");
        u.FamilyId.Should().BeNull();
    }

    [Fact]
    public void User_Create_requires_identity_and_email()
    {
        var a = () => User.Create("", "a@b.com", "A", "B");
        a.Should().Throw<ArgumentException>();
        var b = () => User.Create("id", "  ", "A", "B");
        b.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void User_DisplayName_falls_back_to_email()
    {
        var u = User.Create("id", "solo@x.com", "", "");
        u.DisplayName.Should().Be("solo@x.com");
    }

    [Fact]
    public void User_AssignToFamily_sets_ids_and_rejects_switch()
    {
        var u = User.Create("id", "a@b.com", "A", "B");
        var fam1 = Guid.NewGuid();
        var mem1 = Guid.NewGuid();
        u.AssignToFamily(fam1, mem1);
        u.FamilyId.Should().Be(fam1);
        u.FamilyMemberId.Should().Be(mem1);

        var act = () => u.AssignToFamily(Guid.NewGuid(), Guid.NewGuid());
        act.Should().Throw<InvalidOperationException>();

        u.AssignToFamily(fam1, mem1);
        u.FamilyId.Should().Be(fam1);
    }

    [Fact]
    public void Family_Create_seeds_decline_reasons()
    {
        var f = FamilyOS.Domain.Family.Family.Create("Henderson");
        f.Name.Should().Be("Henderson");
        f.TimeZone.Should().Be("America/Chicago");
        f.Currency.Should().Be("USD");
        f.DeclineReasons.Should().HaveCount(5);
        f.DeclineReasons.Select(r => r.Text).Should().Contain("Can't do it today");
    }

    [Fact]
    public void Family_Create_rejects_empty_name()
    {
        var act = () => FamilyOS.Domain.Family.Family.Create("  ");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Family_AddMember_and_UpdateName()
    {
        var f = FamilyOS.Domain.Family.Family.Create("Home");
        var user = User.Create("u1", "p@x.com", "Pat", "Lee");
        var m = f.AddMember(user, FamilyRole.Owner, "Pat");
        m.Role.Should().Be(FamilyRole.Owner);
        f.Members.Should().ContainSingle();

        var dup = () => f.AddMember(user, FamilyRole.Adult, "Pat2");
        dup.Should().Throw<InvalidOperationException>();

        f.UpdateName("New Home");
        f.Name.Should().Be("New Home");
        var empty = () => f.UpdateName(" ");
        empty.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void DeclineReason_Update_Enable_Disable()
    {
        var reason = DeclineReason.Create(Guid.NewGuid(), "Busy", 1);
        reason.Text.Should().Be("Busy");
        reason.IsEnabled.Should().BeTrue();

        reason.Update("  Really busy  ", 3);
        reason.Text.Should().Be("Really busy");
        reason.SortOrder.Should().Be(3);

        reason.Disable();
        reason.IsEnabled.Should().BeFalse();
        reason.Enable();
        reason.IsEnabled.Should().BeTrue();

        var act = () => DeclineReason.Create(Guid.NewGuid(), "  ", 1);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Family_AddDeclineReason()
    {
        var f = FamilyOS.Domain.Family.Family.Create("Home");
        var before = f.DeclineReasons.Count;
        var r = f.AddDeclineReason("Custom", 99);
        r.Text.Should().Be("Custom");
        f.DeclineReasons.Should().HaveCount(before + 1);
    }
}
