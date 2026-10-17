using FamilyOS.Domain.Requests;
using FluentAssertions;

namespace FamilyOS.Domain.Tests.Requests;

public class RequestQuestionTests
{
    [Fact]
    public void Answer_sets_text_and_time()
    {
        var q = RequestQuestionInstance.Create(Guid.NewGuid(), Guid.NewGuid(), "  When?  ");
        q.QuestionText.Should().Be("When?");
        q.IsAnswered.Should().BeFalse();
        q.Answer("  5pm  ");
        q.IsAnswered.Should().BeTrue();
        q.AnswerText.Should().Be("5pm");
        q.AnsweredAtUtc.Should().NotBeNull();
    }

    [Fact]
    public void Answer_twice_throws()
    {
        var q = RequestQuestionInstance.Create(Guid.NewGuid(), Guid.NewGuid(), "Why?");
        q.Answer("Because");
        var act = () => q.Answer("Again");
        act.Should().Throw<InvalidOperationException>();
    }
}
