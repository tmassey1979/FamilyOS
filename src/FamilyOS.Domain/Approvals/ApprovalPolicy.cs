using FamilyOS.Domain.Common;

namespace FamilyOS.Domain.Approvals;

public class ApprovalPolicy : Entity
{
    public Guid FamilyId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public RequestTypeCode? AppliesToType { get; private set; }
    public ApprovalMode Mode { get; private set; } = ApprovalMode.PolicyDetermined;
    public decimal? MaxAmountForAdult { get; private set; }
    public decimal? MaxAmountForTeen { get; private set; }
    public bool AllowSelfApproval { get; private set; }
    public string? RulesJson { get; private set; }
    public bool IsActive { get; private set; } = true;
    public int Priority { get; private set; }

    private ApprovalPolicy() { }

    public static ApprovalPolicy Create(
        Guid familyId,
        string name,
        ApprovalMode mode = ApprovalMode.PolicyDetermined,
        RequestTypeCode? type = null,
        decimal? maxAdult = 25m,
        decimal? maxTeen = 0m,
        int priority = 0)
    {
        return new ApprovalPolicy
        {
            FamilyId = familyId,
            Name = name,
            Mode = mode,
            AppliesToType = type,
            MaxAmountForAdult = maxAdult,
            MaxAmountForTeen = maxTeen,
            Priority = priority
        };
    }

    public PolicyEvaluationResult Evaluate(FamilyRole approverRole, Guid approverMemberId, Guid requesterMemberId, decimal? amount, RequestTypeCode type)
    {
        if (Mode == ApprovalMode.NeverRequire)
            return PolicyEvaluationResult.AutoApprove("Policy mode is Never Require Approval.");

        if (Mode == ApprovalMode.AlwaysRequire)
            return PolicyEvaluationResult.RequireApproval("Policy mode is Always Require Approval.");

        if (approverMemberId == requesterMemberId && !AllowSelfApproval)
            return PolicyEvaluationResult.Deny("Requester cannot approve their own request.");

        if (approverRole == FamilyRole.Child)
            return PolicyEvaluationResult.Deny("Children cannot approve requests.");

        if (approverRole == FamilyRole.Teen)
            return PolicyEvaluationResult.Deny("Teens cannot approve requests.");

        if (approverRole == FamilyRole.Owner)
            return PolicyEvaluationResult.Allow("Owner can approve everything.");

        if (amount.HasValue && MaxAmountForAdult.HasValue && amount > MaxAmountForAdult)
        {
            return PolicyEvaluationResult.RequireOwner(
                $"Owner approval is required because this purchase exceeds the household's ${MaxAmountForAdult:F0} adult approval limit.");
        }

        return PolicyEvaluationResult.Allow($"Adult approval allowed for amount ${amount ?? 0:F2} under limit ${MaxAmountForAdult ?? 0:F0}.");
    }
}

public class PolicyEvaluationResult
{
    public bool IsAllowed { get; init; }
    public bool RequiresOwner { get; init; }
    public bool AutoApproved { get; init; }
    public string Explanation { get; init; } = string.Empty;

    public static PolicyEvaluationResult Allow(string explanation) =>
        new() { IsAllowed = true, Explanation = explanation };

    public static PolicyEvaluationResult AutoApprove(string explanation) =>
        new() { IsAllowed = true, AutoApproved = true, Explanation = explanation };

    public static PolicyEvaluationResult RequireOwner(string explanation) =>
        new() { IsAllowed = false, RequiresOwner = true, Explanation = explanation };

    public static PolicyEvaluationResult RequireApproval(string explanation) =>
        new() { IsAllowed = false, Explanation = explanation };

    public static PolicyEvaluationResult Deny(string explanation) =>
        new() { IsAllowed = false, Explanation = explanation };
}

public class PolicyOverride : Entity
{
    public Guid FamilyId { get; private set; }
    public Guid RequestId { get; private set; }
    public Guid OverriddenByMemberId { get; private set; }
    public string OriginalResult { get; private set; } = string.Empty;
    public string OverrideDecision { get; private set; } = string.Empty;
    public string? Reason { get; private set; }

    private PolicyOverride() { }

    public static PolicyOverride Create(Guid familyId, Guid requestId, Guid memberId, string original, string decision, string? reason)
    {
        return new PolicyOverride
        {
            FamilyId = familyId,
            RequestId = requestId,
            OverriddenByMemberId = memberId,
            OriginalResult = original,
            OverrideDecision = decision,
            Reason = reason
        };
    }
}
