using FamilyOS.Domain.Approvals;
using FamilyOS.Domain.Common;
using FluentAssertions;

namespace FamilyOS.Domain.Tests.Approvals;

public class PolicyOverrideTests
{
    private static readonly Guid FamilyId = Guid.NewGuid();
    private static readonly Guid RequestId = Guid.NewGuid();
    private static readonly Guid OwnerId = Guid.NewGuid();
    private static readonly Guid AdultId = Guid.NewGuid();
    private static readonly Guid RequesterId = Guid.NewGuid();

    [Fact]
    public void Evaluate_amount_over_adult_limit_requires_owner()
    {
        var policy = ApprovalPolicy.Create(FamilyId, "Purchase", ApprovalMode.PolicyDetermined,
            RequestTypeCode.Purchase, maxAdult: 25m, maxTeen: 0m, priority: 10);

        var result = policy.Evaluate(FamilyRole.Adult, AdultId, RequesterId, 50m, RequestTypeCode.Purchase);
        result.RequiresOwner.Should().BeTrue();
        result.IsAllowed.Should().BeFalse();
        result.Explanation.Should().Contain("25");
    }

    [Fact]
    public void Evaluate_owner_always_allowed()
    {
        var policy = ApprovalPolicy.Create(FamilyId, "Purchase", ApprovalMode.PolicyDetermined,
            RequestTypeCode.Purchase, maxAdult: 25m, priority: 10);

        var result = policy.Evaluate(FamilyRole.Owner, OwnerId, RequesterId, 500m, RequestTypeCode.Purchase);
        result.IsAllowed.Should().BeTrue();
        result.RequiresOwner.Should().BeFalse();
    }

    [Fact]
    public void PolicyOverride_records_original_and_decision()
    {
        var o = PolicyOverride.Create(FamilyId, RequestId, OwnerId,
            "RequiresOwner: over limit", "Approved", "Birthday exception");
        o.OriginalResult.Should().Contain("RequiresOwner");
        o.OverrideDecision.Should().Be("Approved");
        o.Reason.Should().Be("Birthday exception");
        o.OverriddenByMemberId.Should().Be(OwnerId);
    }
}
