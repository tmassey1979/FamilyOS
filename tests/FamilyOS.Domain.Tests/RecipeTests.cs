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
}
