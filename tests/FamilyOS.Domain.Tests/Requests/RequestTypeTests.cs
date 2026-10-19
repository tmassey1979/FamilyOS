using FamilyOS.Domain.Common;
using FamilyOS.Domain.Requests;
using FluentAssertions;

namespace FamilyOS.Domain.Tests.Requests;

public class RequestTypeDefinitionTests
{
    [Fact]
    public void Create_holds_catalog_fields()
    {
        var questions = new List<QuestionDefinition>
        {
            new() { Key = "where", Label = "Where?", Type = QuestionType.Text, Required = true }
        };
        var t = RequestTypeDefinition.Create(
            RequestTypeCode.Ride,
            "Ride",
            questions,
            sortOrder: 10);

        t.Code.Should().Be(RequestTypeCode.Ride);
        t.Name.Should().Be("Ride");
        t.SortOrder.Should().Be(10);
        t.IsEnabled.Should().BeTrue();
        t.GetQuestions().Should().ContainSingle(q => q.Key == "where");
    }
}
