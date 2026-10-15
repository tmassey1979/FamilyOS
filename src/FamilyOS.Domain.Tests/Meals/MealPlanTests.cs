using FamilyOS.Domain.Meals;

namespace FamilyOS.Domain.Tests.Meals;

public class MealPlanTests
{
    [Fact]
    public void ToMonday_Normalizes_Week()
    {
        var wed = new DateTime(2026, 10, 14, 12, 0, 0, DateTimeKind.Utc); // Wednesday
        var monday = MealPlan.ToMonday(wed);
        Assert.Equal(DayOfWeek.Monday, monday.DayOfWeek);
        Assert.Equal(new DateTime(2026, 10, 12), monday);
    }

    [Fact]
    public void UpsertEntry_And_Ingredients()
    {
        var familyId = Guid.NewGuid();
        var plan = MealPlan.Create(familyId, new DateTime(2026, 10, 12));
        var entry = plan.UpsertEntry(
            new DateTime(2026, 10, 13),
            MealSlot.Dinner,
            "Tacos",
            notes: "Kids night",
            ingredientsCsv: "tortillas, ground beef, cheese, tortillas");

        Assert.Equal("Tacos", entry.DishName);
        Assert.Equal(3, entry.IngredientList().Count);
        Assert.Contains("ground beef", entry.IngredientList());

        var updated = plan.UpsertEntry(
            new DateTime(2026, 10, 13),
            MealSlot.Dinner,
            "Burritos",
            ingredientsCsv: "tortillas, beans");
        Assert.Equal(entry.Id, updated.Id);
        Assert.Equal("Burritos", updated.DishName);
        Assert.Equal(2, updated.IngredientList().Count);
    }

    [Fact]
    public void UpsertEntry_Rejects_Outside_Week()
    {
        var plan = MealPlan.Create(Guid.NewGuid(), new DateTime(2026, 10, 12));
        Assert.Throws<InvalidOperationException>(() =>
            plan.UpsertEntry(new DateTime(2026, 10, 20), MealSlot.Lunch, "Soup"));
    }
}
