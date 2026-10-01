using FamilyOS.Domain.Common;
using System.Text.Json;

namespace FamilyOS.Domain.Requests;

public class Request : Entity
{
    public Guid FamilyId { get; private set; }
    public Guid RequesterMemberId { get; private set; }
    public RequestTypeCode Type { get; private set; }
    public RequestStatus Status { get; private set; } = RequestStatus.Draft;
    public string Title { get; private set; } = string.Empty;
    public string? Summary { get; private set; }
    public decimal? Amount { get; private set; }
    public DateTime? NeededByUtc { get; private set; }
    public string? AnswersJson { get; private set; }
    public Guid? CurrentApproverMemberId { get; private set; }
    public string? DenialReason { get; private set; }
    public Guid? ExecutionPlanId { get; private set; }

    private readonly List<RequestHistoryEntry> _history = new();
    public IReadOnlyCollection<RequestHistoryEntry> History => _history.AsReadOnly();

    private readonly List<RequestQuestionInstance> _pendingQuestions = new();
    public IReadOnlyCollection<RequestQuestionInstance> PendingQuestions => _pendingQuestions.AsReadOnly();

    private Request() { }

    public static Request Create(
        Guid familyId,
        Guid requesterMemberId,
        RequestTypeCode type,
        string title,
        Dictionary<string, object?>? answers = null,
        decimal? amount = null,
        DateTime? neededBy = null)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title required.");

        var request = new Request
        {
            FamilyId = familyId,
            RequesterMemberId = requesterMemberId,
            Type = type,
            Title = title.Trim(),
            Amount = amount,
            NeededByUtc = neededBy,
            Status = RequestStatus.Draft,
            AnswersJson = answers != null ? JsonSerializer.Serialize(answers) : null
        };

        request.AddHistory(RequestStatus.Draft, requesterMemberId, "Request created");
        return request;
    }

    public void Submit(Guid memberId)
    {
        if (RequesterMemberId != memberId)
            throw new UnauthorizedAccessException("Only the requester can submit.");
        if (Status != RequestStatus.Draft && Status != RequestStatus.WaitingForInformation)
            throw new InvalidOperationException($"Cannot submit from status {Status}.");

        Status = RequestStatus.Submitted;
        AddHistory(RequestStatus.Submitted, memberId, "Request submitted");
        MarkUpdated();
    }

    public void MoveToReview(Guid reviewerMemberId)
    {
        Status = RequestStatus.UnderReview;
        CurrentApproverMemberId = reviewerMemberId;
        AddHistory(RequestStatus.UnderReview, reviewerMemberId, "Under review");
        MarkUpdated();
    }

    public void AskQuestion(Guid askerMemberId, string questionText)
    {
        if (Status is not (RequestStatus.Submitted or RequestStatus.UnderReview or RequestStatus.WaitingForInformation))
            throw new InvalidOperationException($"Cannot ask question in status {Status}.");

        var q = RequestQuestionInstance.Create(Id, askerMemberId, questionText);
        _pendingQuestions.Add(q);
        Status = RequestStatus.WaitingForInformation;
        AddHistory(RequestStatus.WaitingForInformation, askerMemberId, $"Question asked: {questionText}");
        MarkUpdated();
    }

    public void AnswerQuestion(Guid answererMemberId, Guid questionId, string answer)
    {
        var q = _pendingQuestions.FirstOrDefault(x => x.Id == questionId && !x.IsAnswered)
            ?? throw new InvalidOperationException("Question not found or already answered.");

        if (RequesterMemberId != answererMemberId)
            throw new UnauthorizedAccessException("Only the requester can answer.");

        q.Answer(answer);
        AddHistory(Status, answererMemberId, $"Answered question: {answer}");

        if (_pendingQuestions.All(x => x.IsAnswered))
        {
            Status = RequestStatus.UnderReview;
            AddHistory(RequestStatus.UnderReview, answererMemberId, "All questions answered, back under review");
        }
        MarkUpdated();
    }

    public void Approve(Guid approverMemberId, bool conditional = false)
    {
        if (RequesterMemberId == approverMemberId)
            throw new UnauthorizedAccessException("Requester cannot approve their own request.");

        Status = conditional ? RequestStatus.ConditionallyApproved : RequestStatus.Approved;
        CurrentApproverMemberId = approverMemberId;
        AddHistory(Status, approverMemberId, conditional ? "Conditionally approved" : "Approved");
        MarkUpdated();
    }

    public void Deny(Guid approverMemberId, string reason)
    {
        if (RequesterMemberId == approverMemberId)
            throw new UnauthorizedAccessException("Requester cannot deny their own request.");

        Status = RequestStatus.Denied;
        DenialReason = reason;
        CurrentApproverMemberId = approverMemberId;
        AddHistory(RequestStatus.Denied, approverMemberId, reason);
        MarkUpdated();
    }

    public void MarkExecutable()
    {
        if (Status is not (RequestStatus.Approved or RequestStatus.ConditionallyApproved))
            throw new InvalidOperationException("Request must be approved first.");
        Status = RequestStatus.Executable;
        AddHistory(RequestStatus.Executable, CurrentApproverMemberId ?? Guid.Empty, "Ready for execution");
        MarkUpdated();
    }

    public void LinkExecutionPlan(Guid planId)
    {
        ExecutionPlanId = planId;
        Status = RequestStatus.InExecution;
        AddHistory(RequestStatus.InExecution, CurrentApproverMemberId ?? Guid.Empty, "Execution plan linked");
        MarkUpdated();
    }

    public void Complete()
    {
        Status = RequestStatus.Completed;
        AddHistory(RequestStatus.Completed, CurrentApproverMemberId ?? Guid.Empty, "Request completed");
        MarkUpdated();
    }

    public void SetAnswers(Dictionary<string, object?> answers)
    {
        AnswersJson = JsonSerializer.Serialize(answers);
        MarkUpdated();
    }

    public Dictionary<string, object?> GetAnswers()
    {
        if (string.IsNullOrEmpty(AnswersJson)) return new();
        return JsonSerializer.Deserialize<Dictionary<string, object?>>(AnswersJson) ?? new();
    }

    private void AddHistory(RequestStatus status, Guid actorMemberId, string detail)
    {
        _history.Add(RequestHistoryEntry.Create(Id, status, actorMemberId, detail));
    }
}
