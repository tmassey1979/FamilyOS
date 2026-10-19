using FamilyOS.Domain.Common;
using FamilyOS.Domain.Conditions;
using FamilyOS.Domain.Requests;
using FluentAssertions;

namespace FamilyOS.Domain.Tests.Requests;

public class ConditionalApprovalTests
{
    private static readonly Guid FamilyId = Guid.NewGuid();
    private static readonly Guid Requester = Guid.NewGuid();
    private static readonly Guid Approver = Guid.NewGuid();
    private static readonly Guid Other = Guid.NewGuid();

    private static Request SubmittedPurchase()
    {
        var r = Request.Create(FamilyId, Requester, RequestTypeCode.Purchase, "Bike",
            new Dictionary<string, object?> { ["amount"] = 120 }, 120m, null);
        r.Submit(Requester);
        return r;
    }

    [Fact]
    public void Conditional_approve_sets_ConditionallyApproved()
    {
        var r = SubmittedPurchase();
        r.Approve(Approver, conditional: true);
        r.Status.Should().Be(RequestStatus.ConditionallyApproved);
    }

    [Fact]
    public void PromoteFromConditions_moves_to_Approved()
    {
        var r = SubmittedPurchase();
        r.Approve(Approver, conditional: true);
        r.PromoteFromConditions(Other);
        r.Status.Should().Be(RequestStatus.Approved);
        r.History.Should().Contain(h => h.Detail.Contains("All conditions satisfied"));
    }

    [Fact]
    public void PromoteFromConditions_rejects_when_not_conditional()
    {
        var r = SubmittedPurchase();
        r.Approve(Approver, conditional: false);
        var act = () => r.PromoteFromConditions(Other);
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Condition_Satisfy_records_member_and_time()
    {
        var c = Condition.Create(FamilyId, "Chores done", ConditionType.TaskComplete, requestId: Guid.NewGuid());
        c.Satisfy(Other);
        c.Status.Should().Be(ConditionStatus.Satisfied);
        c.SatisfiedByMemberId.Should().Be(Other);
        c.SatisfiedAtUtc.Should().NotBeNull();
    }
}
