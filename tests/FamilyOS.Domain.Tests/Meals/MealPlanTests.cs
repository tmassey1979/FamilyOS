using FamilyOS.Domain.Meals;
using Xunit;

namespace FamilyOS.Domain.Tests.Meals;

public class MealPlanTests
{
    [Fact]
    public void ToMonday_normalizes_any_day()
    {
        var wed = new DateTime(2026, 10, 14); // Wednesday
        var monday = MealPlan.ToMonday(wed);
        Assert.Equal(DayOfWeek.Monday, monday.DayOfWeek);
        Assert.Equal(new DateTime(2026, 10, 12), monday);
    }

    [Fact]
    public void ToMonday_already_monday_is_stable()
    {
        var mon = new DateTime(2026, 10, 12);
        Assert.Equal(mon, MealPlan.ToMonday(mon));
    }

    [Fact]
    public void UpsertEntry_adds_and_updates_same_slot()
    {
        var plan = MealPlan.Create(Guid.NewGuid(), new DateTime(2026, 10, 12));
        var day = new DateTime(2026, 10, 13);
        var a = plan.UpsertEntry(day, MealSlot.Dinner, "Pasta", ingredientsCsv: "noodles, sauce");
        Assert.Equal("Pasta", a.DishName);
        Assert.Equal(2, a.IngredientList().Count);

        var b = plan.UpsertEntry(day, MealSlot.Dinner, "Tacos", ingredientsCsv: "tortillas");
        Assert.Equal(a.Id, b.Id);
        Assert.Equal("Tacos", b.DishName);
        Assert.Single(b.IngredientList());
    }

    [Fact]
    public void UpsertEntry_rejects_empty_dish()
    {
        var plan = MealPlan.Create(Guid.NewGuid(), new DateTime(2026, 10, 12));
        Assert.Throws<ArgumentException>(() =>
            plan.UpsertEntry(new DateTime(2026, 10, 12), MealSlot.Lunch, "  "));
    }

    [Fact]
    public void UpsertEntry_rejects_day_outside_week()
    {
        var plan = MealPlan.Create(Guid.NewGuid(), new DateTime(2026, 10, 12));
        Assert.Throws<InvalidOperationException>(() =>
            plan.UpsertEntry(new DateTime(2026, 10, 20), MealSlot.Lunch, "Soup"));
    }

    [Fact]
    public void RemoveEntry_soft_deletes()
    {
        var plan = MealPlan.Create(Guid.NewGuid(), new DateTime(2026, 10, 12));
        var e = plan.UpsertEntry(new DateTime(2026, 10, 12), MealSlot.Breakfast, "Eggs");
        plan.RemoveEntry(e.Id);
        Assert.True(e.IsDeleted);
    }

    [Fact]
    public void RemoveEntry_missing_throws()
    {
        var plan = MealPlan.Create(Guid.NewGuid(), new DateTime(2026, 10, 12));
        Assert.Throws<InvalidOperationException>(() => plan.RemoveEntry(Guid.NewGuid()));
    }

    [Fact]
    public void SetNotes_trims_and_clears()
    {
        var plan = MealPlan.Create(Guid.NewGuid(), new DateTime(2026, 10, 12), "  hi  ");
        Assert.Equal("hi", plan.Notes);
        plan.SetNotes("  weekly focus  ");
        Assert.Equal("weekly focus", plan.Notes);
        plan.SetNotes("   ");
        Assert.Null(plan.Notes);
    }
}
