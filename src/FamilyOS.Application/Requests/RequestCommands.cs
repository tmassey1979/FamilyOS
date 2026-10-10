using FamilyOS.Application.Common;
using FamilyOS.Application.Interfaces;
using FamilyOS.Domain.Approvals;
using FamilyOS.Domain.Calendar;
using FamilyOS.Domain.Common;
using FamilyOS.Domain.Execution;
using FamilyOS.Domain.Notifications;
using FamilyOS.Domain.Requests;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FamilyOS.Application.Requests;

public record PendingQuestionDto(Guid Id, string QuestionText, bool IsAnswered, string? Answer);

public record RequestDto(
    Guid Id, RequestTypeCode Type, RequestStatus Status, string Title, string? Summary,
    decimal? Amount, DateTime? NeededByUtc, Guid RequesterMemberId, string RequesterName,
    Guid? CurrentApproverMemberId, string? DenialReason, DateTime CreatedAtUtc,
    Dictionary<string, object?> Answers, Guid? ExecutionPlanId,
    List<PendingQuestionDto> PendingQuestions);

public record RequestTypeDto(Guid Id, RequestTypeCode Code, string Name, string? Description, List<QuestionDefinition> Questions);

public record CreateRequestCommand(
    RequestTypeCode Type, string Title, Dictionary<string, object?> Answers,
    decimal? Amount, DateTime? NeededBy
) : IRequest<RequestDto>;

public record SubmitRequestCommand(Guid RequestId) : IRequest<RequestDto>;
public record AskQuestionCommand(Guid RequestId, string QuestionText) : IRequest<RequestDto>;
public record AnswerQuestionCommand(Guid RequestId, Guid QuestionId, string Answer) : IRequest<RequestDto>;
public record ApproveRequestCommand(Guid RequestId, bool Conditional = false) : IRequest<RequestDto>;
public record DenyRequestCommand(Guid RequestId, string Reason) : IRequest<RequestDto>;
public record GetRequestQuery(Guid RequestId) : IRequest<RequestDto>;
public record GetMyRequestsQuery() : IRequest<List<RequestDto>>;
public record GetApprovalQueueQuery() : IRequest<List<RequestDto>>;
public record GetRequestTypesQuery() : IRequest<List<RequestTypeDto>>;
public record EvaluatePolicyQuery(Guid RequestId) : IRequest<PolicyEvaluationResult>;

public record GenerateExecutionPlanCommand(Guid RequestId) : IRequest<ExecutionPlanDto>;
public record CommitExecutionPlanCommand(Guid PlanId, List<Guid>? DeselectedItemIds) : IRequest<ExecutionPlanDto>;
public record ExecutionPlanDto(Guid Id, Guid RequestId, ExecutionPlanStatus Status, List<ExecutionItemDto> Items);
public record ExecutionItemDto(Guid Id, ExecutionItemType Type, string Title, Guid? AssigneeMemberId, bool IsSelected, Guid? ResultingEntityId);

public class RequestCommandHandlers :
    IRequestHandler<CreateRequestCommand, RequestDto>,
    IRequestHandler<SubmitRequestCommand, RequestDto>,
    IRequestHandler<AskQuestionCommand, RequestDto>,
    IRequestHandler<AnswerQuestionCommand, RequestDto>,
    IRequestHandler<ApproveRequestCommand, RequestDto>,
    IRequestHandler<DenyRequestCommand, RequestDto>,
    IRequestHandler<GetRequestQuery, RequestDto>,
    IRequestHandler<GetMyRequestsQuery, List<RequestDto>>,
    IRequestHandler<GetApprovalQueueQuery, List<RequestDto>>,
    IRequestHandler<GetRequestTypesQuery, List<RequestTypeDto>>,
    IRequestHandler<EvaluatePolicyQuery, PolicyEvaluationResult>,
    IRequestHandler<GenerateExecutionPlanCommand, ExecutionPlanDto>,
    IRequestHandler<CommitExecutionPlanCommand, ExecutionPlanDto>
{
    private readonly IFamilyOsDbContext _db;
    private readonly ICurrentUserService _current;

    public RequestCommandHandlers(IFamilyOsDbContext db, ICurrentUserService current)
    {
        _db = db;
        _current = current;
    }

    public async Task<RequestDto> Handle(CreateRequestCommand cmd, CancellationToken ct)
    {
        await _current.EnsureLoadedAsync(ct);
        EnsureAuth();
        var request = Request.Create(_current.FamilyId!.Value, _current.MemberId!.Value,
            cmd.Type, cmd.Title, cmd.Answers, cmd.Amount, cmd.NeededBy);
        _db.Requests.Add(request);
        await _db.SaveChangesAsync(ct);
        return await ToDto(request, ct);
    }

    public async Task<RequestDto> Handle(SubmitRequestCommand cmd, CancellationToken ct)
    {
        await _current.EnsureLoadedAsync(ct);
        var request = await GetRequest(cmd.RequestId, ct);
        request.Submit(_current.MemberId!.Value);
        await _db.SaveChangesAsync(ct);
        return await ToDto(request, ct);
    }

    public async Task<RequestDto> Handle(AskQuestionCommand cmd, CancellationToken ct)
    {
        await _current.EnsureLoadedAsync(ct);
        EnsureAdult();
        var request = await GetRequest(cmd.RequestId, ct);
        request.AskQuestion(_current.MemberId!.Value, cmd.QuestionText);
        await _db.SaveChangesAsync(ct);
        return await ToDto(request, ct);
    }

    public async Task<RequestDto> Handle(AnswerQuestionCommand cmd, CancellationToken ct)
    {
        await _current.EnsureLoadedAsync(ct);
        var request = await GetRequest(cmd.RequestId, ct);
        request.AnswerQuestion(_current.MemberId!.Value, cmd.QuestionId, cmd.Answer);
        await _db.SaveChangesAsync(ct);
        return await ToDto(request, ct);
    }

    public async Task<RequestDto> Handle(ApproveRequestCommand cmd, CancellationToken ct)
    {
        await _current.EnsureLoadedAsync(ct);
        EnsureAdult();
        var request = await GetRequest(cmd.RequestId, ct);
        request.Approve(_current.MemberId!.Value, cmd.Conditional);
        await _db.SaveChangesAsync(ct);
        return await ToDto(request, ct);
    }

    public async Task<RequestDto> Handle(DenyRequestCommand cmd, CancellationToken ct)
    {
        await _current.EnsureLoadedAsync(ct);
        EnsureAdult();
        var request = await GetRequest(cmd.RequestId, ct);
        request.Deny(_current.MemberId!.Value, cmd.Reason);
        await _db.SaveChangesAsync(ct);
        return await ToDto(request, ct);
    }

    public async Task<RequestDto> Handle(GetRequestQuery query, CancellationToken ct)
    {
        await _current.EnsureLoadedAsync(ct);
        var request = await GetRequest(query.RequestId, ct);
        return await ToDto(request, ct);
    }

    public async Task<List<RequestDto>> Handle(GetMyRequestsQuery query, CancellationToken ct)
    {
        await _current.EnsureLoadedAsync(ct);
        EnsureAuth();
        var list = await _db.Requests.AsNoTracking()
            .Include(r => r.PendingQuestions)
            .Where(r => r.FamilyId == _current.FamilyId && r.RequesterMemberId == _current.MemberId)
            .OrderByDescending(r => r.CreatedAtUtc)
            .ToListAsync(ct);
        var result = new List<RequestDto>();
        foreach (var r in list) result.Add(await ToDto(r, ct));
        return result;
    }

    public async Task<List<RequestDto>> Handle(GetApprovalQueueQuery query, CancellationToken ct)
    {
        await _current.EnsureLoadedAsync(ct);
        EnsureAdult();
        var list = await _db.Requests.AsNoTracking()
            .Include(r => r.PendingQuestions)
            .Where(r => r.FamilyId == _current.FamilyId &&
                        (r.Status == RequestStatus.Submitted
                         || r.Status == RequestStatus.UnderReview
                         || r.Status == RequestStatus.WaitingForInformation))
            .OrderBy(r => r.CreatedAtUtc)
            .ToListAsync(ct);
        var result = new List<RequestDto>();
        foreach (var r in list) result.Add(await ToDto(r, ct));
        return result;
    }

    public async Task<List<RequestTypeDto>> Handle(GetRequestTypesQuery query, CancellationToken ct)
    {
        await _current.EnsureLoadedAsync(ct);
        var types = await _db.RequestTypeDefinitions.AsNoTracking()
            .Where(t => t.IsEnabled)
            .OrderBy(t => t.SortOrder)
            .ToListAsync(ct);
        return types.Select(t => new RequestTypeDto(t.Id, t.Code, t.Name, t.Description, t.GetQuestions())).ToList();
    }

    public async Task<PolicyEvaluationResult> Handle(EvaluatePolicyQuery query, CancellationToken ct)
    {
        await _current.EnsureLoadedAsync(ct);
        var request = await GetRequest(query.RequestId, ct);
        var policy = await _db.ApprovalPolicies.AsNoTracking()
            .Where(p => p.FamilyId == _current.FamilyId && p.IsActive &&
                        (p.AppliesToType == null || p.AppliesToType == request.Type))
            .OrderByDescending(p => p.Priority)
            .FirstOrDefaultAsync(ct);

        if (policy == null)
            return PolicyEvaluationResult.RequireApproval("No policy configured; require adult approval.");

        return policy.Evaluate(_current.Role ?? FamilyRole.Child, _current.MemberId!.Value,
            request.RequesterMemberId, request.Amount, request.Type);
    }

    public async Task<ExecutionPlanDto> Handle(GenerateExecutionPlanCommand cmd, CancellationToken ct)
    {
        await _current.EnsureLoadedAsync(ct);
        EnsureAdult();
        var request = await GetRequest(cmd.RequestId, ct);
        if (request.Status is not (RequestStatus.Approved or RequestStatus.ConditionallyApproved or RequestStatus.Executable))
            throw new BusinessRuleException("Request must be approved before generating a plan.");

        var plan = ExecutionPlan.Create(_current.FamilyId!.Value, request.Id, _current.MemberId!.Value);
        plan.AddItem(ExecutionItemType.Task, $"Execute: {request.Title}", _current.MemberId);
        // Always propose a calendar block for rides; otherwise when NeededBy is set
        if (request.Type == RequestTypeCode.Ride || request.NeededByUtc.HasValue)
            plan.AddItem(ExecutionItemType.CalendarEvent, request.Title, _current.MemberId);

        _db.ExecutionPlans.Add(plan);
        request.LinkExecutionPlan(plan.Id);
        await _db.SaveChangesAsync(ct);
        return ToPlanDto(plan);
    }

    public async Task<ExecutionPlanDto> Handle(CommitExecutionPlanCommand cmd, CancellationToken ct)
    {
        await _current.EnsureLoadedAsync(ct);
        EnsureAdult();
        var plan = await _db.ExecutionPlans
            .Include(p => p.Items)
            .FirstOrDefaultAsync(p => p.Id == cmd.PlanId && p.FamilyId == _current.FamilyId, ct)
            ?? throw new NotFoundException("ExecutionPlan", cmd.PlanId);

        if (cmd.DeselectedItemIds != null)
            foreach (var id in cmd.DeselectedItemIds)
                plan.RemoveItem(id);

        plan.Commit(_current.MemberId!.Value);

        // Materialize selected calendar items into real CalendarEvents
        var request = await _db.Requests.FirstOrDefaultAsync(r => r.Id == plan.RequestId, ct);
        foreach (var item in plan.Items.Where(i => i.IsSelected && i.Type == ExecutionItemType.CalendarEvent))
        {
            var start = request?.NeededByUtc ?? DateTime.UtcNow.AddHours(2);
            var ev = CalendarEvent.Create(
                plan.FamilyId,
                item.Title,
                start,
                _current.MemberId!.Value,
                endUtc: start.AddHours(1),
                allDay: false,
                location: null,
                linkedTaskId: null,
                linkedRequestId: plan.RequestId);
            _db.CalendarEvents.Add(ev);
        }

        await _db.SaveChangesAsync(ct);
        return ToPlanDto(plan);
    }

    private async Task<Request> GetRequest(Guid id, CancellationToken ct)
    {
        EnsureAuth();
        return await _db.Requests
            .Include(r => r.History)
            .Include(r => r.PendingQuestions)
            .FirstOrDefaultAsync(r => r.Id == id && r.FamilyId == _current.FamilyId, ct)
            ?? throw new NotFoundException("Request", id);
    }

    private void EnsureAuth()
    {
        if (!_current.IsAuthenticated || !_current.FamilyId.HasValue || !_current.MemberId.HasValue)
            throw new ForbiddenException("Not authenticated.");
    }

    private void EnsureAdult()
    {
        EnsureAuth();
        if (_current.Role is not (FamilyRole.Owner or FamilyRole.Adult))
            throw new ForbiddenException("Only Adults and Owners can perform this action.");
    }

    private async Task<RequestDto> ToDto(Request r, CancellationToken ct)
    {
        var name = await _db.FamilyMembers.AsNoTracking()
            .Where(m => m.Id == r.RequesterMemberId)
            .Select(m => m.DisplayName)
            .FirstOrDefaultAsync(ct) ?? "Member";

        var questions = r.PendingQuestions
            .Select(q => new PendingQuestionDto(q.Id, q.QuestionText, q.IsAnswered, q.AnswerText))
            .ToList();
        return new RequestDto(r.Id, r.Type, r.Status, r.Title, r.Summary, r.Amount, r.NeededByUtc,
            r.RequesterMemberId, name, r.CurrentApproverMemberId, r.DenialReason, r.CreatedAtUtc, r.GetAnswers(),
            r.ExecutionPlanId, questions);
    }

    private static ExecutionPlanDto ToPlanDto(ExecutionPlan plan) =>
        new(plan.Id, plan.RequestId, plan.Status,
            plan.Items.Select(i => new ExecutionItemDto(i.Id, i.Type, i.Title, i.AssigneeMemberId, i.IsSelected, i.ResultingEntityId)).ToList());
}
