using FamilyOS.Domain.Common;
using FamilyOS.Domain.Requests;
using FluentAssertions;

namespace FamilyOS.Domain.Tests.Requests;

public class RequestWorkflowTests
{
    private static readonly Guid FamilyId = Guid.NewGuid();
    private static readonly Guid TeenId = Guid.NewGuid();
    private static readonly Guid AdultId = Guid.NewGuid();

    private static Request NewRide() =>
        Request.Create(FamilyId, TeenId, RequestTypeCode.Ride, "Ride to soccer",
            new Dictionary<string, object?> { ["where"] = "Community Field" },
            neededBy: DateTime.UtcNow.AddDays(1));

    [Fact]
    public void Create_starts_as_Draft()
    {
        var r = NewRide();
        r.Status.Should().Be(RequestStatus.Draft);
        r.History.Should().ContainSingle();
    }

    [Fact]
    public void Submit_only_by_requester()
    {
        var r = NewRide();
        var act = () => r.Submit(AdultId);
        act.Should().Throw<UnauthorizedAccessException>();

        r.Submit(TeenId);
        r.Status.Should().Be(RequestStatus.Submitted);
    }

    [Fact]
    public void Ask_question_moves_to_WaitingForInformation()
    {
        var r = NewRide();
        r.Submit(TeenId);
        r.AskQuestion(AdultId, "What time do you return?");

        r.Status.Should().Be(RequestStatus.WaitingForInformation);
        r.PendingQuestions.Should().ContainSingle(q => !q.IsAnswered);
    }

    [Fact]
    public void Answer_returns_to_UnderReview_when_all_answered()
    {
        var r = NewRide();
        r.Submit(TeenId);
        r.AskQuestion(AdultId, "Return time?");
        var qid = r.PendingQuestions.First().Id;

        r.AnswerQuestion(TeenId, qid, "5:30 PM");
        r.Status.Should().Be(RequestStatus.UnderReview);
        r.PendingQuestions.Should().OnlyContain(q => q.IsAnswered);
    }

    [Fact]
    public void Approver_cannot_be_requester()
    {
        var r = NewRide();
        r.Submit(TeenId);
        var act = () => r.Approve(TeenId);
        act.Should().Throw<UnauthorizedAccessException>();
    }

    [Fact]
    public void Approve_sets_Approved()
    {
        var r = NewRide();
        r.Submit(TeenId);
        r.Approve(AdultId);
        r.Status.Should().Be(RequestStatus.Approved);
        r.CurrentApproverMemberId.Should().Be(AdultId);
    }

    [Fact]
    public void Conditional_approve_sets_ConditionallyApproved()
    {
        var r = NewRide();
        r.Submit(TeenId);
        r.Approve(AdultId, conditional: true);
        r.Status.Should().Be(RequestStatus.ConditionallyApproved);
    }

    [Fact]
    public void Deny_records_reason()
    {
        var r = NewRide();
        r.Submit(TeenId);
        r.Deny(AdultId, "No driver available");
        r.Status.Should().Be(RequestStatus.Denied);
        r.DenialReason.Should().Be("No driver available");
    }

    [Fact]
    public void MarkExecutable_requires_approval()
    {
        var r = NewRide();
        r.Submit(TeenId);
        var act = () => r.MarkExecutable();
        act.Should().Throw<InvalidOperationException>();

        r.Approve(AdultId);
        r.MarkExecutable();
        r.Status.Should().Be(RequestStatus.Executable);
    }

    [Fact]
    public void History_is_append_only_through_workflow()
    {
        var r = NewRide();
        r.Submit(TeenId);
        r.AskQuestion(AdultId, "Who else?");
        var qid = r.PendingQuestions.First().Id;
        r.AnswerQuestion(TeenId, qid, "Just me");
        r.Approve(AdultId);

        r.History.Count.Should().BeGreaterThanOrEqualTo(5);
        r.History.Select(h => h.TimestampUtc).Should().BeInAscendingOrder();
    }
}
