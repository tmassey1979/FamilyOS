// Covered by RequestWorkflowTests + RequestLifecycleExtraTests — keep a thin smoke suite only.
using FamilyOS.Domain.Common;
using FamilyOS.Domain.Requests;
using FluentAssertions;

namespace FamilyOS.Domain.Tests.Requests;

public class RequestLifecycleSmokeTests
{
    [Fact]
    public void Draft_submit_approve_complete()
    {
        var family = Guid.NewGuid();
        var requester = Guid.NewGuid();
        var approver = Guid.NewGuid();
        var r = Request.Create(family, requester, RequestTypeCode.Personal, "Movie", amount: 20m);
        r.Status.Should().Be(RequestStatus.Draft);
        r.Submit(requester);
        r.Status.Should().Be(RequestStatus.Submitted);
        r.Approve(approver);
        r.Status.Should().Be(RequestStatus.Approved);
        r.MarkExecutable();
        r.LinkExecutionPlan(Guid.NewGuid());
        r.Complete();
        r.Status.Should().Be(RequestStatus.Completed);
    }
}
