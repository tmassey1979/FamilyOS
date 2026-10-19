using FamilyOS.Domain.Requests;
using FluentAssertions;

namespace FamilyOS.Domain.Tests.Requests;

public class RequestTypeDefinitionTests
{
    [Fact]
    public void Definition_holds_catalog_fields()
    {
        var id = Guid.NewGuid();
        var t = new RequestTypeDefinition
        {
            Id = id,
            Code = "ride",
            DisplayName = "Ride",
            Description = "Need a ride",
            RequiresApproval = true,
            AllowsAmount = false,
            AllowsSchedule = true,
            IsActive = true,
            SortOrder = 10
        };

        t.Id.Should().Be(id);
        t.Code.Should().Be("ride");
        t.DisplayName.Should().Be("Ride");
        t.RequiresApproval.Should().BeTrue();
        t.AllowsAmount.Should().BeFalse();
        t.AllowsSchedule.Should().BeTrue();
        t.IsActive.Should().BeTrue();
        t.SortOrder.Should().Be(10);
    }
}
