namespace FamilyOS.Domain.Requests;

public class RequestQuestionInstance
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid RequestId { get; private set; }
    public Guid AskedByMemberId { get; private set; }
    public string QuestionText { get; private set; } = string.Empty;
    public string? AnswerText { get; private set; }
    public bool IsAnswered => !string.IsNullOrEmpty(AnswerText);
    public DateTime AskedAtUtc { get; private set; } = DateTime.UtcNow;
    public DateTime? AnsweredAtUtc { get; private set; }

    private RequestQuestionInstance() { }

    public static RequestQuestionInstance Create(Guid requestId, Guid askedByMemberId, string questionText)
    {
        return new RequestQuestionInstance
        {
            RequestId = requestId,
            AskedByMemberId = askedByMemberId,
            QuestionText = questionText.Trim()
        };
    }

    public void Answer(string answer)
    {
        if (IsAnswered) throw new InvalidOperationException("Already answered.");
        AnswerText = answer.Trim();
        AnsweredAtUtc = DateTime.UtcNow;
    }
}
