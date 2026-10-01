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

namespace FamilyOS.Infrastructure.Persistence;

public class FamilyOsDbContext : DbContext, FamilyOS.Application.Interfaces.IFamilyOsDbContext
{
    public FamilyOsDbContext(DbContextOptions<FamilyOsDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Domain.Family.Family> Families => Set<Domain.Family.Family>();
    public DbSet<FamilyMember> FamilyMembers => Set<FamilyMember>();
    public DbSet<DeclineReason> DeclineReasons => Set<DeclineReason>();
    public DbSet<TaskItem> Tasks => Set<TaskItem>();
    public DbSet<TaskHistoryEntry> TaskHistory => Set<TaskHistoryEntry>();
    public DbSet<TaskTimeSegment> TaskTimeSegments => Set<TaskTimeSegment>();
    public DbSet<RecurrenceDefinition> RecurrenceDefinitions => Set<RecurrenceDefinition>();
    public DbSet<CalendarEvent> CalendarEvents => Set<CalendarEvent>();
    public DbSet<Request> Requests => Set<Request>();
    public DbSet<RequestHistoryEntry> RequestHistory => Set<RequestHistoryEntry>();
    public DbSet<RequestQuestionInstance> RequestQuestions => Set<RequestQuestionInstance>();
    public DbSet<RequestTypeDefinition> RequestTypeDefinitions => Set<RequestTypeDefinition>();
    public DbSet<Condition> Conditions => Set<Condition>();
    public DbSet<ApprovalPolicy> ApprovalPolicies => Set<ApprovalPolicy>();
    public DbSet<PolicyOverride> PolicyOverrides => Set<PolicyOverride>();
    public DbSet<ExecutionPlan> ExecutionPlans => Set<ExecutionPlan>();
    public DbSet<ExecutionItem> ExecutionItems => Set<ExecutionItem>();
    public DbSet<ProcurementItem> ProcurementItems => Set<ProcurementItem>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Cart> Carts => Set<Cart>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<TaskTimingRecord> TaskTimingRecords => Set<TaskTimingRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("familyos");

        modelBuilder.Entity<User>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.ExternalIdentityId).IsUnique();
            e.HasIndex(x => x.Email);
            e.Property(x => x.ExternalIdentityId).HasMaxLength(128).IsRequired();
            e.Property(x => x.Email).HasMaxLength(256).IsRequired();
            e.Property(x => x.FirstName).HasMaxLength(100);
            e.Property(x => x.LastName).HasMaxLength(100);
            e.HasQueryFilter(x => !x.IsDeleted);
        });

        modelBuilder.Entity<Domain.Family.Family>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.HasMany(x => x.Members).WithOne().HasForeignKey(m => m.FamilyId);
            e.HasMany(x => x.DeclineReasons).WithOne().HasForeignKey(d => d.FamilyId);
            e.Navigation(x => x.Members).HasField("_members");
            e.Navigation(x => x.DeclineReasons).HasField("_declineReasons");
            e.HasQueryFilter(x => !x.IsDeleted);
        });

        modelBuilder.Entity<FamilyMember>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.FamilyId, x.UserId }).IsUnique();
            e.Property(x => x.DisplayName).HasMaxLength(200).IsRequired();
            e.Property(x => x.Role).HasConversion<string>().HasMaxLength(20);
            e.HasQueryFilter(x => !x.IsDeleted);
        });

        modelBuilder.Entity<DeclineReason>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Text).HasMaxLength(200).IsRequired();
            e.HasQueryFilter(x => !x.IsDeleted);
        });

        modelBuilder.Entity<TaskItem>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.FamilyId);
            e.HasIndex(x => new { x.FamilyId, x.AssignedToMemberId, x.Status });
            e.Property(x => x.Title).HasMaxLength(300).IsRequired();
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(30);
            e.Property(x => x.Priority).HasConversion<string>().HasMaxLength(20);
            e.HasMany(x => x.History).WithOne().HasForeignKey(h => h.TaskId);
            e.HasMany(x => x.TimeSegments).WithOne().HasForeignKey(s => s.TaskId);
            e.Navigation(x => x.History).HasField("_history");
            e.Navigation(x => x.TimeSegments).HasField("_timeSegments");
            e.HasQueryFilter(x => !x.IsDeleted);
        });

        modelBuilder.Entity<TaskHistoryEntry>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.TaskId);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(30);
            e.Property(x => x.Detail).HasMaxLength(1000);
        });

        modelBuilder.Entity<TaskTimeSegment>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.TaskId);
        });

        modelBuilder.Entity<RecurrenceDefinition>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.FamilyId);
            e.Property(x => x.Title).HasMaxLength(300).IsRequired();
            e.Property(x => x.Frequency).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.DeclinePolicy).HasConversion<string>().HasMaxLength(30);
            e.HasQueryFilter(x => !x.IsDeleted);
        });

        modelBuilder.Entity<CalendarEvent>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.FamilyId, x.StartUtc });
            e.Property(x => x.Title).HasMaxLength(300).IsRequired();
            e.HasQueryFilter(x => !x.IsDeleted);
        });

        modelBuilder.Entity<Request>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.FamilyId);
            e.HasIndex(x => new { x.FamilyId, x.Status });
            e.Property(x => x.Title).HasMaxLength(300).IsRequired();
            e.Property(x => x.Type).HasConversion<string>().HasMaxLength(30);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(30);
            e.Property(x => x.Amount).HasPrecision(18, 2);
            e.HasMany(x => x.History).WithOne().HasForeignKey(h => h.RequestId);
            e.HasMany(x => x.PendingQuestions).WithOne().HasForeignKey(q => q.RequestId);
            e.Navigation(x => x.History).HasField("_history");
            e.Navigation(x => x.PendingQuestions).HasField("_pendingQuestions");
            e.HasQueryFilter(x => !x.IsDeleted);
        });

        modelBuilder.Entity<RequestHistoryEntry>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.RequestId);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(30);
        });

        modelBuilder.Entity<RequestQuestionInstance>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.RequestId);
            e.Property(x => x.QuestionText).HasMaxLength(1000).IsRequired();
            e.Property(x => x.AnswerText).HasMaxLength(2000);
        });

        modelBuilder.Entity<RequestTypeDefinition>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Code).HasConversion<string>().HasMaxLength(30);
            e.Property(x => x.Name).HasMaxLength(100).IsRequired();
            e.HasQueryFilter(x => !x.IsDeleted);
        });

        modelBuilder.Entity<Condition>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.FamilyId);
            e.HasIndex(x => x.RequestId);
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.Type).HasConversion<string>().HasMaxLength(30);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.LogicGroup).HasConversion<string>().HasMaxLength(10);
            e.HasQueryFilter(x => !x.IsDeleted);
        });

        modelBuilder.Entity<ApprovalPolicy>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.FamilyId);
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.Mode).HasConversion<string>().HasMaxLength(30);
            e.Property(x => x.AppliesToType).HasConversion<string>().HasMaxLength(30);
            e.Property(x => x.MaxAmountForAdult).HasPrecision(18, 2);
            e.Property(x => x.MaxAmountForTeen).HasPrecision(18, 2);
            e.HasQueryFilter(x => !x.IsDeleted);
        });

        modelBuilder.Entity<PolicyOverride>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.RequestId);
            e.HasQueryFilter(x => !x.IsDeleted);
        });

        modelBuilder.Entity<ExecutionPlan>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.RequestId).IsUnique();
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.HasMany(x => x.Items).WithOne().HasForeignKey(i => i.ExecutionPlanId);
            e.Navigation(x => x.Items).HasField("_items");
            e.HasQueryFilter(x => !x.IsDeleted);
        });

        modelBuilder.Entity<ExecutionItem>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Type).HasConversion<string>().HasMaxLength(30);
            e.Property(x => x.Title).HasMaxLength(300).IsRequired();
        });

        modelBuilder.Entity<ProcurementItem>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.FamilyId);
            e.HasIndex(x => new { x.FamilyId, x.Status });
            e.Property(x => x.Name).HasMaxLength(300).IsRequired();
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(30);
            e.Property(x => x.EstimatedPrice).HasPrecision(18, 2);
            e.Property(x => x.ActualPrice).HasPrecision(18, 2);
            e.HasQueryFilter(x => !x.IsDeleted);
        });

        modelBuilder.Entity<Product>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.FamilyId);
            e.Property(x => x.Name).HasMaxLength(300).IsRequired();
            e.Property(x => x.TypicalPrice).HasPrecision(18, 2);
            e.HasQueryFilter(x => !x.IsDeleted);
        });

        modelBuilder.Entity<Cart>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.FamilyId);
            e.Property(x => x.StoreName).HasMaxLength(200).IsRequired();
            e.Ignore(x => x.ItemIds);
            e.HasQueryFilter(x => !x.IsDeleted);
        });

        modelBuilder.Entity<Notification>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.RecipientMemberId, x.IsRead });
            e.Property(x => x.Type).HasConversion<string>().HasMaxLength(40);
            e.Property(x => x.Title).HasMaxLength(300).IsRequired();
            e.Property(x => x.Body).HasMaxLength(2000);
            e.HasQueryFilter(x => !x.IsDeleted);
        });

        modelBuilder.Entity<TaskTimingRecord>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.FamilyId, x.TaskCategory });
            e.Property(x => x.TaskCategory).HasMaxLength(100).IsRequired();
        });
    }
}
