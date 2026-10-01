namespace FamilyOS.Domain.Requests;

public class RequestHistoryEntry
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid RequestId { get; private set; }
    public Common.RequestStatus Status { get; private set; }
    public Guid ActorMemberId { get; private set; }
    public string Detail { get; private set; } = string.Empty;
    public DateTime TimestampUtc { get; private set; } = DateTime.UtcNow;

    private RequestHistoryEntry() { }

    public static RequestHistoryEntry Create(Guid requestId, Common.RequestStatus status, Guid actorMemberId, string detail)
    {
        return new RequestHistoryEntry
        {
            RequestId = requestId,
            Status = status,
            ActorMemberId = actorMemberId,
            Detail = detail
        };
    }
}
