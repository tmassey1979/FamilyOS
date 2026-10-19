using FamilyOS.Domain.Meals;
using FluentAssertions;

namespace FamilyOS.Domain.Tests.Meals;

public class RecipeTests
{
    private static readonly Guid FamilyId = Guid.NewGuid();
    private static readonly Guid Creator = Guid.NewGuid();

    [Fact]
    public void Create_trims_and_defaults_servings()
    {
        var r = Recipe.Create(FamilyId, "  Pasta  ", Creator, servings: 0, ingredientsCsv: " eggs , milk ");
        r.Name.Should().Be("Pasta");
        r.Servings.Should().Be(1);
        r.IngredientsCsv.Should().Be("eggs, milk");
        r.CreatedByMemberId.Should().Be(Creator);
    }

    [Fact]
    public void Create_rejects_empty_name()
    {
        var act = () => Recipe.Create(FamilyId, "  ", Creator);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Update_changes_fields()
    {
        var r = Recipe.Create(FamilyId, "Soup", Creator, servings: 2);
        r.Update("Chili", 6, "beans, beef", "Simmer");
        r.Name.Should().Be("Chili");
        r.Servings.Should().Be(6);
        r.IngredientsCsv.Should().Be("beans, beef");
        r.Instructions.Should().Be("Simmer");
    }

    [Fact]
    public void ParseIngredients_splits_and_trims()
    {
        var r = Recipe.Create(FamilyId, "Tacos", Creator, ingredientsCsv: "tortilla, cheese,, salsa");
        r.ParseIngredients().Should().Equal("tortilla", "cheese", "salsa");
    }

    [Fact]
    public void ScaledIngredients_scales_leading_quantities()
    {
        var r = Recipe.Create(FamilyId, "Pancakes", Creator, servings: 2, ingredientsCsv: "2 cup flour, butter, 1 tsp salt");
        // target 4 servings => 2x
        var scaled = r.ScaledIngredients(4);
        scaled.Should().Contain("4 cup flour");
        scaled.Should().Contain("butter"); // no leading number
        scaled.Should().Contain("2 tsp salt");
    }

    [Fact]
    public void ScaledIngredients_same_servings_returns_unscaled()
    {
        var r = Recipe.Create(FamilyId, "Salad", Creator, servings: 4, ingredientsCsv: "2 cup lettuce");
        r.ScaledIngredients(4).Should().Equal("2 cup lettuce");
    }
}
