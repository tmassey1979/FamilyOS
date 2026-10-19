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
        var day = new DateOnly(2026, 10, 13);
        var e = plan.UpsertMeal(day, "Dinner", "Pasta", notes: "garlic");
        plan.Entries.Should().ContainSingle();
        e.Title.Should().Be("Pasta");
        e.Slot.Should().Be("Dinner");
        e.Date.Should().Be(day);
    }

    [Fact]
    public void UpsertMeal_updates_same_day_slot()
    {
        var plan = MealPlan.Create(FamilyId);
        var day = new DateOnly(2026, 10, 13);
        var e1 = plan.UpsertMeal(day, "Dinner", "Pasta");
        var e2 = plan.UpsertMeal(day, "Dinner", "Tacos");
        plan.Entries.Should().ContainSingle();
        e2.Id.Should().Be(e1.Id);
        e2.Title.Should().Be("Tacos");
    }

    [Fact]
    public void RemoveMeal_by_id()
    {
        var plan = MealPlan.Create(FamilyId);
        var e = plan.UpsertMeal(new DateOnly(2026, 10, 12), "Breakfast", "Eggs");
        plan.RemoveMeal(e.Id);
        plan.Entries.Should().BeEmpty();
    }

    [Fact]
    public void UpsertMeal_rejects_empty_dish_or_slot()
    {
        var plan = MealPlan.Create(FamilyId);
        var day = new DateOnly(2026, 10, 12);
        var a = () => plan.UpsertMeal(day, " ", "Eggs");
        a.Should().Throw<ArgumentException>();
        var b = () => plan.UpsertMeal(day, "Lunch", "  ");
        b.Should().Throw<ArgumentException>();
    }
}
