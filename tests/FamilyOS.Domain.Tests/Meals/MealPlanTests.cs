using FamilyOS.Domain.Common;
using FamilyOS.Domain.Meals;
using FluentAssertions;

namespace FamilyOS.Domain.Tests.Meals;

public class MealPlanTests
{
    private static readonly Guid FamilyId = Guid.NewGuid();

    [Theory]
    [InlineData(2026, 10, 14)] // Wed
    [InlineData(2026, 10, 12)] // Mon
    [InlineData(2026, 10, 11)] // Sun
    [InlineData(2026, 10, 17)] // Sat
    public void ToMonday_always_returns_monday(int y, int m, int d)
    {
        var any = new DateTime(y, m, d, 15, 30, 0, DateTimeKind.Utc);
        var monday = MealPlan.ToMonday(any);
        monday.DayOfWeek.Should().Be(DayOfWeek.Monday);
        monday.Kind.Should().Be(DateTimeKind.Utc);
        monday.TimeOfDay.Should().Be(TimeSpan.Zero);
        // Monday is never after the input date
        monday.Date.Should().BeOnOrBefore(any.Date);
        // At most 6 days earlier
        (any.Date - monday.Date).TotalDays.Should().BeInRange(0, 6);
    }

    [Fact]
    public void Create_sets_week_start_to_monday()
    {
        var plan = MealPlan.Create(FamilyId, new DateTime(2026, 10, 15, 0, 0, 0, DateTimeKind.Utc));
        plan.FamilyId.Should().Be(FamilyId);
        plan.WeekStartUtc.DayOfWeek.Should().Be(DayOfWeek.Monday);
        plan.Entries.Should().BeEmpty();
    }

    [Fact]
    public void UpsertEntry_adds_and_updates()
    {
        var plan = MealPlan.Create(FamilyId, DateTime.UtcNow);
        var day = new DateTime(2026, 10, 13, 0, 0, 0, DateTimeKind.Utc);
        var e1 = plan.UpsertEntry(day, MealSlot.Dinner, "Pasta", notes: "garlic");
        plan.Entries.Should().ContainSingle();
        e1.DishName.Should().Be("Pasta");
        e1.Slot.Should().Be(MealSlot.Dinner);

        var e2 = plan.UpsertEntry(day, MealSlot.Dinner, "Tacos");
        plan.Entries.Should().ContainSingle();
        e2.Id.Should().Be(e1.Id);
        e2.DishName.Should().Be("Tacos");
    }

    [Fact]
    public void RemoveEntry_soft_deletes()
    {
        var plan = MealPlan.Create(FamilyId, DateTime.UtcNow);
        var e = plan.UpsertEntry(DateTime.UtcNow.Date, MealSlot.Breakfast, "Eggs");
        plan.RemoveEntry(e.Id);
        e.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public void UpsertEntry_rejects_empty_dish()
    {
        var plan = MealPlan.Create(FamilyId, DateTime.UtcNow);
        var act = () => plan.UpsertEntry(DateTime.UtcNow.Date, MealSlot.Lunch, "  ");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void AllIngredients_skips_deleted()
    {
        var plan = MealPlan.Create(FamilyId, DateTime.UtcNow);
        var day = DateTime.UtcNow.Date;
        var e = plan.UpsertEntry(day, MealSlot.Dinner, "Pasta", ingredientsCsv: "pasta, garlic");
        plan.UpsertEntry(day, MealSlot.Lunch, "Salad", ingredientsCsv: "lettuce");
        plan.RemoveEntry(e.Id);
        plan.AllIngredients().Should().Equal("lettuce");
    }
}
