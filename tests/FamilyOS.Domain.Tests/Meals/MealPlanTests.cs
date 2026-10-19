using FamilyOS.Domain.Common;
using FamilyOS.Domain.Meals;
using FluentAssertions;

namespace FamilyOS.Domain.Tests.Meals;

public class MealPlanTests
{
    private static readonly Guid FamilyId = Guid.NewGuid();

    [Fact]
    public void ToMonday_normalizes_to_week_start()
    {
        // 2026-10-14 is Wednesday
        var wed = new DateTime(2026, 10, 14, 15, 30, 0, DateTimeKind.Utc);
        var monday = MealPlan.ToMonday(wed);
        monday.Should().Be(new DateTime(2026, 10, 12, 0, 0, 0, DateTimeKind.Utc));
        monday.DayOfWeek.Should().Be(DayOfWeek.Monday);
    }

    [Fact]
    public void Create_sets_week_start_to_monday()
    {
        var plan = MealPlan.Create(FamilyId, new DateTime(2026, 10, 15, 0, 0, 0, DateTimeKind.Utc));
        plan.FamilyId.Should().Be(FamilyId);
        plan.WeekStartUtc.DayOfWeek.Should().Be(DayOfWeek.Monday);
        plan.Entries.Should().BeEmpty();
    }

    [Fact]
    public void UpsertEntry_adds_and_updates()
    {
        var plan = MealPlan.Create(FamilyId, DateTime.UtcNow);
        var day = new DateTime(2026, 10, 13, 0, 0, 0, DateTimeKind.Utc);
        var e1 = plan.UpsertEntry(day, MealSlot.Dinner, "Pasta", notes: "garlic");
        plan.Entries.Should().ContainSingle();
        e1.DishName.Should().Be("Pasta");
        e1.Slot.Should().Be(MealSlot.Dinner);

        var e2 = plan.UpsertEntry(day, MealSlot.Dinner, "Tacos");
        plan.Entries.Should().ContainSingle();
        e2.Id.Should().Be(e1.Id);
        e2.DishName.Should().Be("Tacos");
    }

    [Fact]
    public void RemoveEntry_soft_deletes()
    {
        var plan = MealPlan.Create(FamilyId, DateTime.UtcNow);
        var e = plan.UpsertEntry(DateTime.UtcNow.Date, MealSlot.Breakfast, "Eggs");
        plan.RemoveEntry(e.Id);
        e.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public void UpsertEntry_rejects_empty_dish()
    {
        var plan = MealPlan.Create(FamilyId, DateTime.UtcNow);
        var act = () => plan.UpsertEntry(DateTime.UtcNow.Date, MealSlot.Lunch, "  ");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void AllIngredients_skips_deleted()
    {
        var plan = MealPlan.Create(FamilyId, DateTime.UtcNow);
        var day = DateTime.UtcNow.Date;
        var e = plan.UpsertEntry(day, MealSlot.Dinner, "Pasta", ingredientsCsv: "pasta, garlic");
        plan.UpsertEntry(day, MealSlot.Lunch, "Salad", ingredientsCsv: "lettuce");
        plan.RemoveEntry(e.Id);
        plan.AllIngredients().Should().Equal("lettuce");
    }
}
