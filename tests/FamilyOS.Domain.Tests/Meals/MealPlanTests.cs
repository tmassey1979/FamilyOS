using FamilyOS.Domain.Meals;
using FluentAssertions;

namespace FamilyOS.Domain.Tests.Meals;

public class MealPlanTests
{
    private static readonly Guid FamilyId = Guid.NewGuid();

    [Fact]
    public void ToMonday_snaps_week_to_monday()
    {
        // Wednesday 2026-10-14 -> Monday 2026-10-12
        var wed = new DateTime(2026, 10, 14, 15, 0, 0, DateTimeKind.Utc);
        MealPlan.ToMonday(wed).Should().Be(new DateTime(2026, 10, 12));

        var monday = new DateTime(2026, 10, 12);
        MealPlan.ToMonday(monday).Should().Be(monday);

        // Sunday maps back to prior Monday
        var sun = new DateTime(2026, 10, 18);
        MealPlan.ToMonday(sun).Should().Be(new DateTime(2026, 10, 12));
    }

    [Fact]
    public void Create_normalizes_week_start()
    {
        var plan = MealPlan.Create(FamilyId, new DateTime(2026, 10, 15)); // Thu
        plan.WeekStartUtc.Should().Be(new DateTime(2026, 10, 12));
        plan.FamilyId.Should().Be(FamilyId);
        plan.Entries.Should().BeEmpty();
    }

    [Fact]
    public void UpsertEntry_adds_and_updates_same_day_slot()
    {
        var plan = MealPlan.Create(FamilyId, new DateTime(2026, 10, 12));
        var day = new DateTime(2026, 10, 13);
        var e1 = plan.UpsertEntry(day, MealSlot.Dinner, "Pasta", "garlic", "noodles, sauce");
        plan.Entries.Should().ContainSingle();
        e1.DishName.Should().Be("Pasta");

        var e2 = plan.UpsertEntry(day, MealSlot.Dinner, "Tacos", null, "tortilla");
        plan.Entries.Should().ContainSingle();
        e2.Id.Should().Be(e1.Id);
        e2.DishName.Should().Be("Tacos");
    }

    [Fact]
    public void RemoveEntry_by_id()
    {
        var plan = MealPlan.Create(FamilyId, new DateTime(2026, 10, 12));
        var e = plan.UpsertEntry(new DateTime(2026, 10, 12), MealSlot.Breakfast, "Eggs");
        plan.RemoveEntry(e.Id);
        plan.Entries.Should().BeEmpty();
    }

    [Fact]
    public void RemoveSlot_by_day_and_slot()
    {
        var plan = MealPlan.Create(FamilyId, new DateTime(2026, 10, 12));
        plan.UpsertEntry(new DateTime(2026, 10, 14), MealSlot.Lunch, "Soup");
        plan.RemoveSlot(new DateTime(2026, 10, 14), MealSlot.Lunch);
        plan.Entries.Should().BeEmpty();
    }

    [Fact]
    public void AllIngredients_dedupes_across_entries()
    {
        var plan = MealPlan.Create(FamilyId, new DateTime(2026, 10, 12));
        plan.UpsertEntry(new DateTime(2026, 10, 12), MealSlot.Dinner, "A", ingredientsCsv: "Eggs, Milk");
        plan.UpsertEntry(new DateTime(2026, 10, 13), MealSlot.Breakfast, "B", ingredientsCsv: "milk, Bread");
        var all = plan.AllIngredients();
        all.Should().BeEquivalentTo(new[] { "Eggs", "Milk", "Bread" });
    }
}
