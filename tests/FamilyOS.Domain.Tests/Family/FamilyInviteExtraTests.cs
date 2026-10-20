using FamilyOS.Domain.Common;
using FamilyOS.Domain.Family;
using FluentAssertions;

namespace FamilyOS.Domain.Tests.Family;

public class FamilyInviteExtraTests
{
    private static readonly Guid FamilyId = Guid.NewGuid();
    private static readonly Guid Creator = Guid.NewGuid();

    [Fact]
    public void ValidDays_clamped_1_to_90()
    {
        var shortInv = FamilyInvite.Create(FamilyId, Creator, FamilyRole.Adult, validDays: 0);
        shortInv.ExpiresAtUtc.Should().BeCloseTo(DateTime.UtcNow.AddDays(1), TimeSpan.FromMinutes(1));

        var longInv = FamilyInvite.Create(FamilyId, Creator, FamilyRole.Adult, validDays: 200);
        longInv.ExpiresAtUtc.Should().BeCloseTo(DateTime.UtcNow.AddDays(90), TimeSpan.FromMinutes(1));
    }

    [Fact]
    public void Revoke_after_accept_is_noop()
    {
        var inv = FamilyInvite.Create(FamilyId, Creator, FamilyRole.Adult);
        inv.Accept(Guid.NewGuid());
        inv.Revoke();
        inv.IsRevoked.Should().BeFalse();
        inv.AcceptedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public void EmailHint_normalized()
    {
        var inv = FamilyInvite.Create(FamilyId, Creator, FamilyRole.Adult, emailHint: "  Foo@Bar.COM  ");
        inv.EmailHint.Should().Be("foo@bar.com");
    }
}
