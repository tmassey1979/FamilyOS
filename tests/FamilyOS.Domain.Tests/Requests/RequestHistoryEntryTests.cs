using FamilyOS.Domain.Common;
using FamilyOS.Domain.Requests;
using FluentAssertions;

namespace FamilyOS.Domain.Tests.Requests;

public class RequestHistoryEntryTests
{
    [Fact]
    public void Create_records_fields()
    {
        var requestId = Guid.NewGuid();
        var actor = Guid.NewGuid();
        var h = RequestHistoryEntry.Create(requestId, RequestStatus.Submitted, actor, "Submitted");
        h.RequestId.Should().Be(requestId);
        h.Status.Should().Be(RequestStatus.Submitted);
        h.ActorMemberId.Should().Be(actor);
        h.Detail.Should().Be("Submitted");
    }
}
