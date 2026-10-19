using FamilyOS.Domain.Common;
using FamilyOS.Domain.Requests;
using FluentAssertions;

namespace FamilyOS.Domain.Tests.Requests;

public class RequestLifecycleTests
{
    private static readonly Guid FamilyId = Guid.NewGuid();
    private static readonly Guid Requester = Guid.NewGuid();
    private static readonly Guid Approver = Guid.NewGuid();

    private static Request NewDraft(string title = "Movie night") =>
        Request.Create(FamilyId, Requester, RequestTypeCode.Personal, title, amount: 25m);

    [Fact]
    public void Create_starts_as_draft()
    {
        var r = NewDraft();
        r.Status.Should().Be(RequestStatus.Draft);
        r.FamilyId.Should().Be(FamilyId);
        r.RequesterMemberId.Should().Be(Requester);
        r.Amount.Should().Be(25m);
        r.History.Should().ContainSingle();
    }

    [Fact]
    public void Create_rejects_empty_title()
    {
        var act = () => Request.Create(FamilyId, Requester, RequestTypeCode.Personal, "  ");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Submit_approve_complete_happy_path()
    {
        var r = NewDraft();
        r.Submit(Requester);
        r.Status.Should().Be(RequestStatus.Submitted);

        r.MoveToReview(Approver);
        r.Status.Should().Be(RequestStatus.UnderReview);
        r.CurrentApproverMemberId.Should().Be(Approver);

        r.Approve(Approver);
        r.Status.Should().Be(RequestStatus.Approved);

        r.MarkExecutable();
        r.Status.Should().Be(RequestStatus.Executable);

        var planId = Guid.NewGuid();
        r.LinkExecutionPlan(planId);
        r.Status.Should().Be(RequestStatus.InExecution);
        r.ExecutionPlanId.Should().Be(planId);

        r.Complete();
        r.Status.Should().Be(RequestStatus.Completed);
    }

    [Fact]
    public void Conditional_approve_then_promote()
    {
        var r = NewDraft();
        r.Submit(Requester);
        r.Approve(Approver, conditional: true);
        r.Status.Should().Be(RequestStatus.ConditionallyApproved);

        r.PromoteFromConditions(Approver);
        r.Status.Should().Be(RequestStatus.Approved);
    }

    [Fact]
    public void Deny_sets_reason()
    {
        var r = NewDraft();
        r.Submit(Requester);
        r.Deny(Approver, "Budget exhausted");
        r.Status.Should().Be(RequestStatus.Denied);
        r.DenialReason.Should().Be("Budget exhausted");
    }

    [Fact]
    public void Requester_cannot_approve_or_deny_own()
    {
        var r = NewDraft();
        r.Submit(Requester);
        var approve = () => r.Approve(Requester);
        var deny = () => r.Deny(Requester, "no");
        approve.Should().Throw<UnauthorizedAccessException>();
        deny.Should().Throw<UnauthorizedAccessException>();
    }

    [Fact]
    public void Ask_and_answer_question_returns_to_review()
    {
        var r = NewDraft();
        r.Submit(Requester);
        r.AskQuestion(Approver, "Which theater?");
        r.Status.Should().Be(RequestStatus.WaitingForInformation);
        r.PendingQuestions.Should().ContainSingle();

        var qId = r.PendingQuestions.First().Id;
        r.AnswerQuestion(Requester, qId, "AMC downtown");
        r.Status.Should().Be(RequestStatus.UnderReview);
        r.PendingQuestions.First().IsAnswered.Should().BeTrue();
    }

    [Fact]
    public void Cancel_by_requester_from_submitted()
    {
        var r = NewDraft();
        r.Submit(Requester);
        r.Cancel(Requester, isOwner: false, reason: "Changed mind");
        r.Status.Should().Be(RequestStatus.Cancelled);
    }

    [Fact]
    public void Cancel_rejected_after_approve()
    {
        var r = NewDraft();
        r.Submit(Requester);
        r.Approve(Approver);
        var act = () => r.Cancel(Requester, isOwner: false);
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void UpdateDraft_only_requester_while_draft()
    {
        var r = NewDraft();
        r.UpdateDraft(Requester, title: "Concert", amount: 40m);
        r.Title.Should().Be("Concert");
        r.Amount.Should().Be(40m);

        var other = () => r.UpdateDraft(Approver, title: "Hack");
        other.Should().Throw<UnauthorizedAccessException>();
    }

    [Fact]
    public void SetAnswers_round_trips()
    {
        var r = NewDraft();
        r.SetAnswers(new Dictionary<string, object?> { ["where"] = "Park" });
        r.GetAnswers()["where"]!.ToString().Should().Be("Park");
    }
}
