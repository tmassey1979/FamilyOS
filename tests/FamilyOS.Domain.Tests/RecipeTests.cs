using FamilyOS.Domain.Meals;
using Xunit;

namespace FamilyOS.Domain.Tests;

public class RecipeTests
{
    private static readonly Guid FamilyId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");

    [Fact]
    public void Create_normalizes_name_and_ingredients()
    {
        var r = Recipe.Create(FamilyId, "  Pasta  ", 2, " flour, Eggs, flour ", "Mix");
        Assert.Equal("Pasta", r.Name);
        Assert.Equal(2, r.Servings);
        Assert.Equal("flour, Eggs", r.IngredientsCsv);
        Assert.Equal(2, r.IngredientList().Count);
    }

    [Fact]
    public void Create_rejects_empty_name()
    {
        Assert.Throws<ArgumentException>(() => Recipe.Create(FamilyId, "  "));
    }

    [Fact]
    public void Create_rejects_zero_servings()
    {
        Assert.Throws<ArgumentException>(() => Recipe.Create(FamilyId, "Soup", 0));
    }

    [Fact]
    public void Update_changes_fields()
    {
        var r = Recipe.Create(FamilyId, "A", 4, "x");
        r.Update("B", 6, "y, z", "Bake");
        Assert.Equal("B", r.Name);
        Assert.Equal(6, r.Servings);
        Assert.Equal("y, z", r.IngredientsCsv);
        Assert.Equal("Bake", r.Instructions);
    }

    [Fact]
    public void SoftDelete_marks_deleted()
    {
        var r = Recipe.Create(FamilyId, "X");
        r.SoftDelete();
        Assert.True(r.IsDeleted);
    }

    [Fact]
    public void ScaleIngredients_doubles_numeric_qty()
    {
        var r = Recipe.Create(FamilyId, "Pancakes", 2, "1 cup flour, 2 eggs, salt");
        var scaled = r.ScaleIngredientsForServings(4);
        Assert.Equal(3, scaled.Count);
        Assert.Equal("2 cup flour", scaled[0]);
        Assert.Equal("4 eggs", scaled[1]);
        Assert.Equal("salt", scaled[2]);
    }

    [Fact]
    public void ScaleIngredients_same_servings_is_identity()
    {
        var r = Recipe.Create(FamilyId, "Tea", 1, "1 bag");
        var scaled = r.ScaleIngredientsForServings(1);
        Assert.Single(scaled);
        Assert.Equal("1 bag", scaled[0]);
    }

    [Fact]
    public void ScaleIngredients_rejects_zero_target()
    {
        var r = Recipe.Create(FamilyId, "X", 2, "1 cup");
        Assert.Throws<ArgumentException>(() => r.ScaleIngredientsForServings(0));
    }
}
