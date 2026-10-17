using FamilyOS.Domain.Common;
using FamilyOS.Domain.Requests;
using FluentAssertions;

namespace FamilyOS.Domain.Tests.Requests;

public class RequestLifecycleExtraTests
{
    private static readonly Guid FamilyId = Guid.NewGuid();
    private static readonly Guid Requester = Guid.NewGuid();
    private static readonly Guid Approver = Guid.NewGuid();

    private static Request Money() =>
        Request.Create(FamilyId, Requester, RequestTypeCode.Money, "Snack money", amount: 12.5m);

    [Fact]
    public void PromoteFromConditions_after_conditional_approval()
    {
        var r = Money();
        r.Submit(Requester);
        r.Approve(Approver, conditional: true);
        r.PromoteFromConditions(Approver);
        r.Status.Should().Be(RequestStatus.Approved);
        r.History.Should().Contain(h => h.Detail.Contains("conditions satisfied"));
    }

    [Fact]
    public void PromoteFromConditions_rejects_non_conditional()
    {
        var r = Money();
        r.Submit(Requester);
        r.Approve(Approver);
        var act = () => r.PromoteFromConditions(Approver);
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void LinkExecutionPlan_and_Complete()
    {
        var r = Money();
        r.Submit(Requester);
        r.Approve(Approver);
        r.MarkExecutable();
        var planId = Guid.NewGuid();
        r.LinkExecutionPlan(planId);
        r.Status.Should().Be(RequestStatus.InExecution);
        r.ExecutionPlanId.Should().Be(planId);
        r.Complete();
        r.Status.Should().Be(RequestStatus.Completed);
    }

    [Fact]
    public void SetAnswers_and_GetAnswers_roundtrip()
    {
        var r = Money();
        r.SetAnswers(new Dictionary<string, object?> { ["purpose"] = "lunch" });
        var answers = r.GetAnswers();
        answers.Should().ContainKey("purpose");
    }

    [Fact]
    public void UpdateDraft_rejects_other_member()
    {
        var r = Money();
        var act = () => r.UpdateDraft(Approver, title: "Nope");
        act.Should().Throw<UnauthorizedAccessException>();
    }

    [Fact]
    public void UpdateDraft_can_set_amount_and_clear_needed_by()
    {
        var r = Request.Create(FamilyId, Requester, RequestTypeCode.Ride, "Ride",
            neededBy: DateTime.UtcNow.AddDays(2));
        r.UpdateDraft(Requester, amount: 0, clearNeededBy: true);
        r.NeededByUtc.Should().BeNull();
    }

    [Fact]
    public void MoveToReview_sets_approver()
    {
        var r = Money();
        r.Submit(Requester);
        r.MoveToReview(Approver);
        r.Status.Should().Be(RequestStatus.UnderReview);
        r.CurrentApproverMemberId.Should().Be(Approver);
    }
}
