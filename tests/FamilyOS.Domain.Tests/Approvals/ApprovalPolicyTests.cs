using FamilyOS.Domain.Approvals;
using FamilyOS.Domain.Common;
using FluentAssertions;

namespace FamilyOS.Domain.Tests.Approvals;

public class ApprovalPolicyTests
{
    private static readonly Guid FamilyId = Guid.NewGuid();
    private static readonly Guid OwnerId = Guid.NewGuid();
    private static readonly Guid AdultId = Guid.NewGuid();
    private static readonly Guid TeenId = Guid.NewGuid();
    private static readonly Guid RequesterId = Guid.NewGuid();

    private static ApprovalPolicy DefaultPurchase() =>
        ApprovalPolicy.Create(FamilyId, "Purchase", ApprovalMode.PolicyDetermined,
            RequestTypeCode.Purchase, maxAdult: 25m, maxTeen: 0m, priority: 10);

    [Fact]
    public void Owner_can_approve_any_amount()
    {
        var policy = DefaultPurchase();
        var result = policy.Evaluate(FamilyRole.Owner, OwnerId, RequesterId, 500m, RequestTypeCode.Purchase);
        result.IsAllowed.Should().BeTrue();
        result.Explanation.Should().Contain("Owner");
    }

    [Fact]
    public void Adult_can_approve_under_limit()
    {
        var policy = DefaultPurchase();
        var result = policy.Evaluate(FamilyRole.Adult, AdultId, RequesterId, 18m, RequestTypeCode.Purchase);
        result.IsAllowed.Should().BeTrue();
        result.RequiresOwner.Should().BeFalse();
    }

    [Fact]
    public void Adult_requires_owner_above_limit()
    {
        var policy = DefaultPurchase();
        var result = policy.Evaluate(FamilyRole.Adult, AdultId, RequesterId, 80m, RequestTypeCode.Purchase);
        result.IsAllowed.Should().BeFalse();
        result.RequiresOwner.Should().BeTrue();
        result.Explanation.Should().Contain("25");
    }

    [Fact]
    public void Teen_cannot_approve()
    {
        var policy = DefaultPurchase();
        var result = policy.Evaluate(FamilyRole.Teen, TeenId, RequesterId, 5m, RequestTypeCode.Purchase);
        result.IsAllowed.Should().BeFalse();
        result.Explanation.Should().Contain("Teen");
    }

    [Fact]
    public void Child_cannot_approve()
    {
        var policy = DefaultPurchase();
        var result = policy.Evaluate(FamilyRole.Child, Guid.NewGuid(), RequesterId, 5m, RequestTypeCode.Purchase);
        result.IsAllowed.Should().BeFalse();
    }

    [Fact]
    public void Requester_cannot_self_approve_by_default()
    {
        var policy = DefaultPurchase();
        var result = policy.Evaluate(FamilyRole.Adult, RequesterId, RequesterId, 10m, RequestTypeCode.Purchase);
        result.IsAllowed.Should().BeFalse();
        result.Explanation.Should().Contain("own request");
    }

    [Fact]
    public void NeverRequire_auto_approves()
    {
        var policy = ApprovalPolicy.Create(FamilyId, "Grocery", ApprovalMode.NeverRequire, RequestTypeCode.Grocery);
        var result = policy.Evaluate(FamilyRole.Adult, AdultId, RequesterId, 100m, RequestTypeCode.Grocery);
        result.IsAllowed.Should().BeTrue();
        result.AutoApproved.Should().BeTrue();
    }

    [Fact]
    public void AlwaysRequire_blocks_auto_path()
    {
        var policy = ApprovalPolicy.Create(FamilyId, "Strict", ApprovalMode.AlwaysRequire);
        var result = policy.Evaluate(FamilyRole.Adult, AdultId, RequesterId, 1m, RequestTypeCode.General);
        result.IsAllowed.Should().BeFalse();
        result.AutoApproved.Should().BeFalse();
    }
}
