using FamilyOS.Domain.Approvals;
using FamilyOS.Domain.Calendar;
using FamilyOS.Domain.Conditions;
using FamilyOS.Domain.Execution;
using FamilyOS.Domain.Family;
using FamilyOS.Domain.Notifications;
using FamilyOS.Domain.Procurement;
using FamilyOS.Domain.Requests;
using FamilyOS.Domain.Tasks;
using FamilyOS.Domain.TimeIntelligence;
using Microsoft.EntityFrameworkCore;

namespace FamilyOS.Application.Interfaces;

public interface IFamilyOsDbContext
{
    DbSet<User> Users { get; }
    DbSet<Domain.Family.Family> Families { get; }
    DbSet<FamilyMember> FamilyMembers { get; }
    DbSet<DeclineReason> DeclineReasons { get; }
    DbSet<TaskItem> Tasks { get; }
    DbSet<TaskHistoryEntry> TaskHistory { get; }
    DbSet<TaskTimeSegment> TaskTimeSegments { get; }
    DbSet<RecurrenceDefinition> RecurrenceDefinitions { get; }
    DbSet<CalendarEvent> CalendarEvents { get; }
    DbSet<Request> Requests { get; }
    DbSet<RequestHistoryEntry> RequestHistory { get; }
    DbSet<RequestQuestionInstance> RequestQuestions { get; }
    DbSet<RequestTypeDefinition> RequestTypeDefinitions { get; }
    DbSet<Condition> Conditions { get; }
    DbSet<ApprovalPolicy> ApprovalPolicies { get; }
    DbSet<PolicyOverride> PolicyOverrides { get; }
    DbSet<ExecutionPlan> ExecutionPlans { get; }
    DbSet<ExecutionItem> ExecutionItems { get; }
    DbSet<ProcurementItem> ProcurementItems { get; }
    DbSet<Product> Products { get; }
    DbSet<Cart> Carts { get; }
    DbSet<Notification> Notifications { get; }
    DbSet<TaskTimingRecord> TaskTimingRecords { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
