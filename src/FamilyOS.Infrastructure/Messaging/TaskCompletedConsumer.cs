using FamilyOS.Contracts.Events;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace FamilyOS.Infrastructure.Messaging;

/// <summary>Sample consumer — proves the bus is live; later steps can drive Pulse / analytics.</summary>
public sealed class TaskCompletedConsumer : IConsumer<TaskCompleted>
{
    private readonly ILogger<TaskCompletedConsumer> _log;

    public TaskCompletedConsumer(ILogger<TaskCompletedConsumer> log) => _log = log;

    public Task Consume(ConsumeContext<TaskCompleted> context)
    {
        var m = context.Message;
        _log.LogInformation(
            "TaskCompleted event: TaskId={TaskId} FamilyId={FamilyId} MemberId={MemberId} Duration={Duration}",
            m.TaskId, m.FamilyId, m.MemberId, m.ActualDurationMinutes);
        return Task.CompletedTask;
    }
}
