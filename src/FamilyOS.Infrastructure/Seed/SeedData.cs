using FamilyOS.Domain.Approvals;
using FamilyOS.Domain.Calendar;
using FamilyOS.Domain.Common;
using FamilyOS.Domain.Family;
using FamilyOS.Domain.Procurement;
using FamilyOS.Domain.Requests;
using FamilyOS.Domain.Tasks;
using FamilyOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FamilyOS.Infrastructure.Seed;

public static class SeedData
{
    public static async Task InitializeAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FamilyOsDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<FamilyOsDbContext>>();

        await db.Database.MigrateAsync();

        // Additive schema for quiet hours without a full EF migration cycle (#46).
        await EnsureQuietHoursColumnsAsync(db, logger);

        // Additive schema for push device tokens (#27).
        await EnsureDeviceRegistrationsTableAsync(db, logger);

        // Idempotent: fill any missing request-type catalog entries on every boot.
        await EnsureRequestTypesAsync(db, logger);

        if (await db.Families.AnyAsync())
        {
            logger.LogInformation("Database already seeded.");
            return;
        }

        logger.LogInformation("Seeding Family OS development data...");

        var family = Family.Create("The Hendersons", "America/Chicago", "USD");
        db.Families.Add(family);
        await db.SaveChangesAsync();

        var terryUser = User.Create("terry.owner", "terry@familyos.local", "Terry", "Owner");
        var michelleUser = User.Create("michelle.adult", "michelle@familyos.local", "Michelle", "Adult");
        var miaUser = User.Create("mia.teen", "mia@familyos.local", "Mia", "Teen");
        var eliUser = User.Create("eli.child", "eli@familyos.local", "Eli", "Child");
        db.Users.AddRange(terryUser, michelleUser, miaUser, eliUser);
        await db.SaveChangesAsync();

        var terry = family.AddMember(terryUser, FamilyRole.Owner, "Terry");
        var michelle = family.AddMember(michelleUser, FamilyRole.Adult, "Michelle");
        var mia = family.AddMember(miaUser, FamilyRole.Teen, "Mia");
        var eli = family.AddMember(eliUser, FamilyRole.Child, "Eli");
        db.FamilyMembers.AddRange(terry, michelle, mia, eli);
        await db.SaveChangesAsync();

        terryUser.AssignToFamily(family.Id, terry.Id);
        michelleUser.AssignToFamily(family.Id, michelle.Id);
        miaUser.AssignToFamily(family.Id, mia.Id);
        eliUser.AssignToFamily(family.Id, eli.Id);
        await db.SaveChangesAsync();

        db.ApprovalPolicies.Add(ApprovalPolicy.Create(
            family.Id, "Default Purchase Policy", ApprovalMode.PolicyDetermined,
            RequestTypeCode.Purchase, maxAdult: 25m, maxTeen: 0m, priority: 10));
        db.ApprovalPolicies.Add(ApprovalPolicy.Create(
            family.Id, "Grocery Auto-Approve", ApprovalMode.NeverRequire,
            RequestTypeCode.Grocery, priority: 5));
        db.ApprovalPolicies.Add(ApprovalPolicy.Create(
            family.Id, "Ride Always Require Adult", ApprovalMode.AlwaysRequire,
            RequestTypeCode.Ride, priority: 20));
        db.ApprovalPolicies.Add(ApprovalPolicy.Create(
            family.Id, "Default Policy", ApprovalMode.PolicyDetermined,
            null, maxAdult: 50m, priority: 0));

        SeedRequestTypes(db);
        await db.SaveChangesAsync();

        var trash = TaskItem.Create(family.Id, "Take out trash", terry.Id, category: "Chores", dueDate: DateTime.UtcNow.Date);
        trash.Assign(eli.Id, terry.Id);
        var laundry = TaskItem.Create(family.Id, "Fold laundry", terry.Id, category: "Chores", dueDate: DateTime.UtcNow.Date);
        laundry.Assign(mia.Id, terry.Id);
        laundry.Accept(mia.Id);
        var kitchen = TaskItem.Create(family.Id, "Clean kitchen", michelle.Id, category: "Chores", dueDate: DateTime.UtcNow.Date.AddDays(1));
        kitchen.Assign(michelle.Id, michelle.Id);
        kitchen.Accept(michelle.Id);
        var pickup = TaskItem.Create(family.Id, "Pick up Eli from school", michelle.Id, category: "Transport",
            dueDate: DateTime.UtcNow.Date, calendarBacked: true);
        pickup.Assign(michelle.Id, michelle.Id);
        pickup.Accept(michelle.Id);
        pickup.SetPlannedTimes(new TimeOnly(15, 30), new TimeOnly(16, 0), 30);
        db.Tasks.AddRange(trash, laundry, kitchen, pickup);

        db.RecurrenceDefinitions.Add(RecurrenceDefinition.Create(
            family.Id, "Take out trash", RecurrenceFrequency.Weekly, 1, DayOfWeek.Thursday, eli.Id, "Chores"));

        db.CalendarEvents.Add(CalendarEvent.Create(
            family.Id, "Pick up Eli", DateTime.UtcNow.Date.AddHours(15).AddMinutes(30),
            michelle.Id, DateTime.UtcNow.Date.AddHours(16), location: "Lincoln Elementary"));
        db.CalendarEvents.Add(CalendarEvent.Create(
            family.Id, "Dinner", DateTime.UtcNow.Date.AddHours(18),
            terry.Id, DateTime.UtcNow.Date.AddHours(19)));
        db.CalendarEvents.Add(CalendarEvent.Create(
            family.Id, "Soccer practice", DateTime.UtcNow.Date.AddDays(1).AddHours(16),
            michelle.Id, DateTime.UtcNow.Date.AddDays(1).AddHours(17).AddMinutes(30),
            location: "Community Field"));

        var rideRequest = Request.Create(family.Id, mia.Id, RequestTypeCode.Ride, "Ride to soccer practice",
            new Dictionary<string, object?> { ["who"] = "Mia", ["where"] = "Community Field" },
            neededBy: DateTime.UtcNow.Date.AddDays(1).AddHours(16));
        rideRequest.Submit(mia.Id);
        db.Requests.Add(rideRequest);

        db.ProcurementItems.Add(ProcurementItem.Create(family.Id, "Milk", michelle.Id, brand: "Prairie Farms", size: "1 gallon", category: "Dairy", estimatedPrice: 4.29m, preferredStore: "Walmart"));
        db.ProcurementItems.Add(ProcurementItem.Create(family.Id, "Eggs", michelle.Id, brand: "Great Value", size: "Dozen", category: "Dairy", estimatedPrice: 2.99m, preferredStore: "Walmart"));
        db.ProcurementItems.Add(ProcurementItem.Create(family.Id, "Bread", michelle.Id, brand: "Sara Lee", category: "Bakery", estimatedPrice: 3.49m, preferredStore: "Walmart"));

        var milk = Product.Create(family.Id, "Milk", "Prairie Farms", "1 gallon", "Dairy");
        milk.RecordPurchase(4.29m, "Walmart");
        db.Products.Add(milk);

        db.Carts.Add(Cart.Create(family.Id, "Walmart", michelle.Id));

        await db.SaveChangesAsync();
        logger.LogInformation("Seed data created successfully for family {FamilyId}", family.Id);
    }

    private static void SeedRequestTypes(FamilyOsDbContext db) =>
        AddMissingRequestTypes(db, existingCodes: new HashSet<RequestTypeCode>());

    private static async Task EnsureRequestTypesAsync(FamilyOsDbContext db, ILogger logger)
    {
        var existing = await db.RequestTypeDefinitions.Select(x => x.Code).ToListAsync();
        var before = existing.Count;
        AddMissingRequestTypes(db, existing.ToHashSet());
        if (db.ChangeTracker.HasChanges())
        {
            await db.SaveChangesAsync();
            var after = await db.RequestTypeDefinitions.CountAsync();
            logger.LogInformation("Request type catalog updated: {Before} → {After}", before, after);
        }
    }

    /// <summary>Additive schema for #46 without a full EF migration cycle.</summary>
    private static async Task EnsureQuietHoursColumnsAsync(FamilyOsDbContext db, ILogger logger)
    {
        try
        {
            // Use regular string (not raw) so JSON transport cannot mangle quotes.
            await db.Database.ExecuteSqlRawAsync(
                "ALTER TABLE familyos.\"FamilyMembers\" ADD COLUMN IF NOT EXISTS \"QuietHoursEnabled\" boolean NOT NULL DEFAULT false;" +
                "ALTER TABLE familyos.\"FamilyMembers\" ADD COLUMN IF NOT EXISTS \"QuietHoursStartMinutes\" integer NULL;" +
                "ALTER TABLE familyos.\"FamilyMembers\" ADD COLUMN IF NOT EXISTS \"QuietHoursEndMinutes\" integer NULL;");
            logger.LogInformation("Quiet hours columns ensured on FamilyMembers");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not ensure quiet hours columns (may already exist or non-Postgres)");
        }
    }

    /// <summary>Additive schema for device push tokens (#27) without a full EF migration cycle.</summary>
    private static async Task EnsureDeviceRegistrationsTableAsync(FamilyOsDbContext db, ILogger logger)
    {
        try
        {
            await db.Database.ExecuteSqlRawAsync(
                "CREATE TABLE IF NOT EXISTS familyos.\"DeviceRegistrations\" (" +
                "\"Id\" uuid NOT NULL, " +
                "\"FamilyId\" uuid NOT NULL, " +
                "\"MemberId\" uuid NOT NULL, " +
                "\"Token\" character varying(512) NOT NULL, " +
                "\"Platform\" character varying(40) NOT NULL, " +
                "\"DeviceName\" character varying(120) NULL, " +
                "\"LastSeenAtUtc\" timestamp with time zone NOT NULL, " +
                "\"IsActive\" boolean NOT NULL, " +
                "\"CreatedAtUtc\" timestamp with time zone NOT NULL, " +
                "\"UpdatedAtUtc\" timestamp with time zone NULL, " +
                "\"IsDeleted\" boolean NOT NULL DEFAULT false, " +
                "CONSTRAINT \"PK_DeviceRegistrations\" PRIMARY KEY (\"Id\"));" +
                "CREATE UNIQUE INDEX IF NOT EXISTS \"IX_DeviceRegistrations_Token\" ON familyos.\"DeviceRegistrations\" (\"Token\");" +
                "CREATE INDEX IF NOT EXISTS \"IX_DeviceRegistrations_MemberId_IsActive\" ON familyos.\"DeviceRegistrations\" (\"MemberId\", \"IsActive\");");
            logger.LogInformation("DeviceRegistrations table ensured");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not ensure DeviceRegistrations table (may already exist or non-Postgres)");
        }
    }

    private static void AddMissingRequestTypes(FamilyOsDbContext db, HashSet<RequestTypeCode> existingCodes)
    {
        void Add(RequestTypeCode code, string name, List<QuestionDefinition> qs, int sortOrder)
        {
            if (existingCodes.Contains(code)) return;
            db.RequestTypeDefinitions.Add(RequestTypeDefinition.Create(code, name, qs, sortOrder: sortOrder));
            existingCodes.Add(code);
        }

        Add(RequestTypeCode.Ride, "Ride", new List<QuestionDefinition>
        {
            new() { Key = "who", Label = "Who needs the ride?", Type = QuestionType.PersonSelection, Required = true, SortOrder = 1 },
            new() { Key = "where", Label = "Where are you going?", Type = QuestionType.Location, Required = true, SortOrder = 2 },
            new() { Key = "leave", Label = "When do you need to leave?", Type = QuestionType.DateTime, Required = true, SortOrder = 3 },
            new() { Key = "return", Label = "When will you return?", Type = QuestionType.DateTime, Required = false, SortOrder = 4 },
            new() { Key = "needRideHome", Label = "Do you need a ride home?", Type = QuestionType.YesNo, Required = true, SortOrder = 5 },
        }, 1);

        Add(RequestTypeCode.Money, "Money", new List<QuestionDefinition>
        {
            new() { Key = "amount", Label = "Amount", Type = QuestionType.MoneyAmount, Required = true, SortOrder = 1 },
            new() { Key = "purpose", Label = "Purpose", Type = QuestionType.Text, Required = true, SortOrder = 2 },
        }, 2);

        Add(RequestTypeCode.Purchase, "Purchase", new List<QuestionDefinition>
        {
            new() { Key = "item", Label = "What do you want to buy?", Type = QuestionType.Text, Required = true, SortOrder = 1 },
            new() { Key = "amount", Label = "Estimated cost", Type = QuestionType.MoneyAmount, Required = true, SortOrder = 2 },
            new() { Key = "store", Label = "Preferred store", Type = QuestionType.Text, Required = false, SortOrder = 3 },
        }, 3);

        Add(RequestTypeCode.Grocery, "Groceries", new List<QuestionDefinition>
        {
            new() { Key = "items", Label = "Items needed", Type = QuestionType.Text, Required = true, SortOrder = 1 },
        }, 4);

        Add(RequestTypeCode.SnackFood, "Snack / food", new List<QuestionDefinition>
        {
            new() { Key = "what", Label = "What snack or food?", Type = QuestionType.Text, Required = true, SortOrder = 1 },
            new() { Key = "amount", Label = "Estimated cost", Type = QuestionType.MoneyAmount, Required = false, SortOrder = 2 },
        }, 5);

        Add(RequestTypeCode.School, "School", new List<QuestionDefinition>
        {
            new() { Key = "what", Label = "What do you need for school?", Type = QuestionType.Text, Required = true, SortOrder = 1 },
            new() { Key = "due", Label = "When is it needed?", Type = QuestionType.Date, Required = false, SortOrder = 2 },
            new() { Key = "amount", Label = "Cost (if any)", Type = QuestionType.MoneyAmount, Required = false, SortOrder = 3 },
        }, 6);

        Add(RequestTypeCode.Medical, "Medical", new List<QuestionDefinition>
        {
            new() { Key = "what", Label = "What is needed?", Type = QuestionType.Text, Required = true, SortOrder = 1 },
            new() { Key = "when", Label = "Appointment / needed by", Type = QuestionType.DateTime, Required = false, SortOrder = 2 },
            new() { Key = "urgent", Label = "Is this urgent?", Type = QuestionType.Text, Required = true, SortOrder = 3 },
        }, 7);

        Add(RequestTypeCode.Household, "Household", new List<QuestionDefinition>
        {
            new() { Key = "what", Label = "What household need?", Type = QuestionType.Text, Required = true, SortOrder = 1 },
            new() { Key = "amount", Label = "Estimated cost", Type = QuestionType.MoneyAmount, Required = false, SortOrder = 2 },
        }, 8);

        Add(RequestTypeCode.Personal, "Personal", new List<QuestionDefinition>
        {
            new() { Key = "what", Label = "What do you need?", Type = QuestionType.Text, Required = true, SortOrder = 1 },
            new() { Key = "why", Label = "Why is it important?", Type = QuestionType.Text, Required = false, SortOrder = 2 },
            new() { Key = "amount", Label = "Cost (if any)", Type = QuestionType.MoneyAmount, Required = false, SortOrder = 3 },
        }, 9);

        Add(RequestTypeCode.General, "Something else", new List<QuestionDefinition>
        {
            new() { Key = "what", Label = "What kind of help do you need?", Type = QuestionType.Text, Required = true, SortOrder = 1 },
        }, 10);
    }
}
