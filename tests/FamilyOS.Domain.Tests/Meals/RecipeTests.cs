using FamilyOS.Domain.Meals;
using FluentAssertions;

namespace FamilyOS.Domain.Tests.Meals;

public class RecipeTests
{
    private static readonly Guid FamilyId = Guid.NewGuid();

    [Fact]
    public void Create_trims_and_requires_servings()
    {
        var r = Recipe.Create(FamilyId, "  Pasta  ", servings: 2, ingredientsCsv: " eggs , milk ");
        r.Name.Should().Be("Pasta");
        r.Servings.Should().Be(2);
        r.IngredientsCsv.Should().Be("eggs, milk");
    }

    [Fact]
    public void Create_rejects_empty_name_or_zero_servings()
    {
        var a = () => Recipe.Create(FamilyId, "  ");
        a.Should().Throw<ArgumentException>();
        var b = () => Recipe.Create(FamilyId, "Soup", servings: 0);
        b.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Update_changes_fields()
    {
        var r = Recipe.Create(FamilyId, "Soup", servings: 2);
        r.Update("Chili", 6, "beans, beef", "Simmer");
        r.Name.Should().Be("Chili");
        r.Servings.Should().Be(6);
        r.IngredientsCsv.Should().Be("beans, beef");
        r.Instructions.Should().Be("Simmer");
    }

    [Fact]
    public void IngredientList_splits_and_trims()
    {
        var r = Recipe.Create(FamilyId, "Tacos", ingredientsCsv: "tortilla, cheese,, salsa");
        r.IngredientList().Should().Equal("tortilla", "cheese", "salsa");
    }

    [Fact]
    public void ScaleIngredientsForServings_scales_leading_quantities()
    {
        var r = Recipe.Create(FamilyId, "Pancakes", servings: 2, ingredientsCsv: "2 cup flour, butter, 1 tsp salt");
        var scaled = r.ScaleIngredientsForServings(4);
        scaled.Should().Contain("4 cup flour");
        scaled.Should().Contain("butter");
        scaled.Should().Contain("2 tsp salt");
    }

    [Fact]
    public void ScaleIngredientsForServings_same_servings_unchanged()
    {
        var r = Recipe.Create(FamilyId, "Salad", servings: 4, ingredientsCsv: "2 cup lettuce");
        r.ScaleIngredientsForServings(4).Should().Equal("2 cup lettuce");
    }
}
