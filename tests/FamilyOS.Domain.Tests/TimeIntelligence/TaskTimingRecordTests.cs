using FamilyOS.Domain.TimeIntelligence;
using FluentAssertions;

namespace FamilyOS.Domain.Tests.TimeIntelligence;

public class TaskTimingRecordTests
{
    [Fact]
    public void Create_normalizes_title()
    {
        var r = TaskTimingRecord.Create(
            Guid.NewGuid(), "Chores", 25, Guid.NewGuid(),
            titleNormalized: "  Take Out Trash  ", sourceTaskId: Guid.NewGuid());
        r.TaskCategory.Should().Be("Chores");
        r.DurationMinutes.Should().Be(25);
        r.TaskTitleNormalized.Should().Be("take out trash");
        r.SourceTaskId.Should().NotBeNull();
    }
}

public class TaskTimingSummaryTests
{
    [Theory]
    [InlineData(0, "Insufficient")]
    [InlineData(1, "Insufficient")]
    [InlineData(2, "Low")]
    [InlineData(5, "Medium")]
    [InlineData(20, "High")]
    public void Confidence_bands(int samples, string expected)
    {
        var s = new TaskTimingSummary { SampleCount = samples, AverageMinutes = 10, Category = "X" };
        s.Confidence.Should().Be(expected);
    }
}
