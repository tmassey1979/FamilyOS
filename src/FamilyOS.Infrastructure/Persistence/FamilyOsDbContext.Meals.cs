using FamilyOS.Domain.Meals;
using Microsoft.EntityFrameworkCore;

namespace FamilyOS.Infrastructure.Persistence;

public partial class FamilyOsDbContext
{
    public DbSet<MealPlan> MealPlans => Set<MealPlan>();
    public DbSet<MealEntry> MealEntries => Set<MealEntry>();

    private static void ConfigureMeals(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MealPlan>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.FamilyId, x.WeekStartUtc }).IsUnique();
            e.HasMany(x => x.Entries).WithOne().HasForeignKey(x => x.MealPlanId);
            e.Navigation(x => x.Entries).HasField("_entries");
            e.HasQueryFilter(x => !x.IsDeleted);
        });

        modelBuilder.Entity<MealEntry>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.MealPlanId, x.DayUtc, x.Slot });
            e.HasIndex(x => x.FamilyId);
            e.Property(x => x.DishName).HasMaxLength(300).IsRequired();
            e.Property(x => x.Slot).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.IngredientsCsv).HasMaxLength(2000);
            e.Property(x => x.Notes).HasMaxLength(1000);
            e.HasQueryFilter(x => !x.IsDeleted);
        });
    }
}
