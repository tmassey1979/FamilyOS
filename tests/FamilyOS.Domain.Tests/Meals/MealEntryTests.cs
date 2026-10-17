using FamilyOS.Domain.Meals;
using FluentAssertions;

namespace FamilyOS.Domain.Tests.Meals;

public class MealEntryTests
{
    [Fact]
    public void Create_and_Update_normalizes_ingredients()
    {
        var e = MealEntry.Create(
            Guid.NewGuid(), Guid.NewGuid(), new DateTime(2026, 10, 12),
            MealSlot.Dinner, "  Pasta  ", notes: "  ok  ", ingredientsCsv: " flour, Eggs, flour ");
        e.DishName.Should().Be("Pasta");
        e.Notes.Should().Be("ok");
        e.IngredientList().Should().HaveCount(2);

        e.Update("Tacos", null, "tortilla, cheese, tortilla");
        e.DishName.Should().Be("Tacos");
        e.Notes.Should().BeNull();
        e.IngredientList().Should().BeEquivalentTo(new[] { "tortilla", "cheese" });
    }

    [Fact]
    public void Update_rejects_blank_dish()
    {
        var e = MealEntry.Create(Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow, MealSlot.Lunch, "Soup");
        var act = () => e.Update("  ", null, null);
        act.Should().Throw<ArgumentException>();
    }
}
