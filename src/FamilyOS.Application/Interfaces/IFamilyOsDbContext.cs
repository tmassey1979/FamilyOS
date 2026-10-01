using FamilyOS.Domain.Approvals;
using FamilyOS.Domain.Calendar;
using FamilyOS.Domain.Conditions;
using FamilyOS.Domain.Execution;
using FamilyOS.Domain.Family;
using FamilyOS.Domain.Notifications;
using FamilyOS.Domain.Procurement;
using FamilyOS.Domain.Requests;
using FamilyOS.Domain.Tasks;
using Microsoft.EntityFrameworkCore;

namespace FamilyOS.Application.Interfaces;

public interface IFamilyOsDbContext
{
    DbSet<Family> Families { get; }
    DbSet<FamilyMember> FamilyMembers { get; }
    DbSet<User> Users { get; }
    DbSet<DeclineReason> DeclineReasons { get; }
    DbSet<TaskItem> Tasks { get; }
    DbSet<RecurrenceDefinition> RecurrenceDefinitions { get; }
    DbSet<CalendarEvent> CalendarEvents { get; }
    DbSet<Request> Requests { get; }
    DbSet<RequestTypeDefinition> RequestTypeDefinitions { get; }
    DbSet<ApprovalPolicy> ApprovalPolicies { get; }
    DbSet<Condition> Conditions { get; }
    DbSet<ExecutionPlan> ExecutionPlans { get; }
    DbSet<ProcurementItem> ProcurementItems { get; }
    DbSet<Product> Products { get; }
    DbSet<Cart> Carts { get; }
    DbSet<Notification> Notifications { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
