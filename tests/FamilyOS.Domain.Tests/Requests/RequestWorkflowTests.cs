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
        r.AskQuestion(AdultId, "What time?");
        r.Status.Should().Be(RequestStatus.WaitingForInformation);
        r.PendingQuestions.Should().ContainSingle();
    }

    [Fact]
    public void Answer_question_by_requester_returns_to_review()
    {
        var r = NewRide();
        r.Submit(TeenId);
        r.AskQuestion(AdultId, "What time?");
        var qid = r.PendingQuestions.First().Id;
        r.AnswerQuestion(TeenId, qid, "5pm");
        r.Status.Should().Be(RequestStatus.UnderReview);
    }

    [Fact]
    public void Approve_cannot_be_self()
    {
        var r = NewRide();
        r.Submit(TeenId);
        var act = () => r.Approve(TeenId);
        act.Should().Throw<UnauthorizedAccessException>();
    }

    [Fact]
    public void Approve_and_conditional()
    {
        var r = NewRide();
        r.Submit(TeenId);
        r.Approve(AdultId);
        r.Status.Should().Be(RequestStatus.Approved);

        var r2 = NewRide();
        r2.Submit(TeenId);
        r2.Approve(AdultId, conditional: true);
        r2.Status.Should().Be(RequestStatus.ConditionallyApproved);
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

    [Fact]
    public void Cancel_by_requester_withdraws_submitted_request()
    {
        var r = NewRide();
        r.Submit(TeenId);
        r.Cancel(TeenId, isOwner: false, reason: "Changed plans");
        r.Status.Should().Be(RequestStatus.Cancelled);
        r.History.Should().Contain(h => h.Detail.Contains("Withdrawn"));
    }

    [Fact]
    public void Cancel_by_owner_allowed()
    {
        var r = NewRide();
        r.Submit(TeenId);
        r.Cancel(AdultId, isOwner: true);
        r.Status.Should().Be(RequestStatus.Cancelled);
    }

    [Fact]
    public void Cancel_by_non_requester_non_owner_throws()
    {
        var r = NewRide();
        r.Submit(TeenId);
        var stranger = Guid.NewGuid();
        var act = () => r.Cancel(stranger, isOwner: false);
        act.Should().Throw<UnauthorizedAccessException>();
    }

    [Fact]
    public void Cancel_after_approval_throws()
    {
        var r = NewRide();
        r.Submit(TeenId);
        r.Approve(AdultId);
        var act = () => r.Cancel(TeenId, isOwner: false);
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void UpdateDraft_changes_title_and_records_history()
    {
        var r = NewRide();
        r.UpdateDraft(TeenId, title: "Ride to practice", answers: null, amount: null, neededBy: null);
        r.Title.Should().Be("Ride to practice");
        r.History.Should().Contain(h => h.Detail.Contains("Draft updated"));
    }

    [Fact]
    public void Cancel_from_draft_records_history()
    {
        var r = NewRide();
        r.Cancel(TeenId, isOwner: false, reason: "Changed plans");
        r.Status.Should().Be(RequestStatus.Cancelled);
        r.History.Should().Contain(h => h.Status == RequestStatus.Cancelled);
    }
}
