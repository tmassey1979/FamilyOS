using FamilyOS.Domain.Common;
using FamilyOS.Domain.Meals;
using FluentAssertions;

namespace FamilyOS.Domain.Tests.Meals;

public class MealPlanTests
{
    private static readonly Guid FamilyId = Guid.NewGuid();

    [Fact]
    public void Create_empty_plan()
    {
        var plan = MealPlan.Create(FamilyId);
        plan.FamilyId.Should().Be(FamilyId);
        plan.Entries.Should().BeEmpty();
    }

    [Fact]
    public void UpsertMeal_adds_entry()
    {
        var plan = MealPlan.Create(FamilyId);
        var day = new DateTime(2026, 10, 13, 0, 0, 0, DateTimeKind.Utc);
        var e = plan.UpsertMeal(day, MealSlot.Dinner, "Pasta", notes: "garlic");
        plan.Entries.Should().ContainSingle();
        e.DishName.Should().Be("Pasta");
        e.Slot.Should().Be(MealSlot.Dinner);
        e.DayUtc.Date.Should().Be(day.Date);
        e.Notes.Should().Be("garlic");
    }

    [Fact]
    public void UpsertMeal_updates_same_day_slot()
    {
        var plan = MealPlan.Create(FamilyId);
        var day = new DateTime(2026, 10, 13, 0, 0, 0, DateTimeKind.Utc);
        var e1 = plan.UpsertMeal(day, MealSlot.Dinner, "Pasta");
        var e2 = plan.UpsertMeal(day, MealSlot.Dinner, "Tacos");
        plan.Entries.Should().ContainSingle();
        e2.Id.Should().Be(e1.Id);
        e2.DishName.Should().Be("Tacos");
    }

    [Fact]
    public void RemoveMeal_by_id()
    {
        var plan = MealPlan.Create(FamilyId);
        var e = plan.UpsertMeal(new DateTime(2026, 10, 12, 0, 0, 0, DateTimeKind.Utc), MealSlot.Breakfast, "Eggs");
        plan.RemoveMeal(e.Id);
        plan.Entries.Should().BeEmpty();
    }

    [Fact]
    public void UpsertMeal_rejects_empty_dish()
    {
        var plan = MealPlan.Create(FamilyId);
        var day = new DateTime(2026, 10, 12, 0, 0, 0, DateTimeKind.Utc);
        var act = () => plan.UpsertMeal(day, MealSlot.Lunch, "  ");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void AllIngredients_aggregates_unique()
    {
        var plan = MealPlan.Create(FamilyId);
        var day = new DateTime(2026, 10, 13, 0, 0, 0, DateTimeKind.Utc);
        plan.UpsertMeal(day, MealSlot.Dinner, "Pasta", ingredientsCsv: "pasta, garlic");
        plan.UpsertMeal(day, MealSlot.Lunch, "Salad", ingredientsCsv: "garlic, lettuce");
        plan.AllIngredients().Should().BeEquivalentTo(new[] { "garlic", "lettuce", "pasta" });
    }
}
