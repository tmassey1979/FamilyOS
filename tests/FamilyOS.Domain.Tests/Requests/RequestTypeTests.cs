using FamilyOS.Domain.Requests;
using FluentAssertions;

namespace FamilyOS.Domain.Tests.Requests;

public class RequestTypeTests
{
    [Fact]
    public void Create_sets_code_name_and_flags()
    {
        var familyId = Guid.NewGuid();
        var t = RequestType.Create(familyId, "  ride  ", "  Ride Request  ", requiresApproval: true, requiresAmount: false);
        t.FamilyId.Should().Be(familyId);
        t.Code.Should().Be("ride");
        t.Name.Should().Be("Ride Request");
        t.RequiresApproval.Should().BeTrue();
        t.RequiresAmount.Should().BeFalse();
        t.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Create_rejects_empty_code_or_name()
    {
        var fid = Guid.NewGuid();
        var a = () => RequestType.Create(fid, "", "Name");
        a.Should().Throw<ArgumentException>();
        var b = () => RequestType.Create(fid, "code", " ");
        b.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Deactivate_and_Activate()
    {
        var t = RequestType.Create(Guid.NewGuid(), "snack", "Snack");
        t.Deactivate();
        t.IsActive.Should().BeFalse();
        t.Activate();
        t.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Rename_updates_display_name()
    {
        var t = RequestType.Create(Guid.NewGuid(), "school", "School");
        t.Rename("School Trip");
        t.Name.Should().Be("School Trip");
        t.Code.Should().Be("school"); // code unchanged
    }
}
