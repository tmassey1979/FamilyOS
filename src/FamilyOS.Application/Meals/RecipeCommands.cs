using FamilyOS.Application.Common;
using FamilyOS.Application.Interfaces;
using FamilyOS.Domain.Common;
using FamilyOS.Domain.Meals;
using FamilyOS.Domain.Procurement;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FamilyOS.Application.Meals;

public record RecipeDto(
    Guid Id, string Name, int Servings, string? IngredientsCsv, string? Instructions);

public record ListRecipesQuery() : IRequest<List<RecipeDto>>;
public record GetRecipeQuery(Guid Id) : IRequest<RecipeDto>;
public record CreateRecipeCommand(
    string Name, int Servings = 4, string? IngredientsCsv = null, string? Instructions = null) : IRequest<RecipeDto>;
public record UpdateRecipeCommand(
    Guid Id, string Name, int Servings, string? IngredientsCsv, string? Instructions) : IRequest<RecipeDto>;
public record DeleteRecipeCommand(Guid Id) : IRequest;
public record ApplyRecipeToMealCommand(
    Guid RecipeId, DateTime DayUtc, string Slot, DateTime? WeekStartUtc = null) : IRequest<MealEntryDto>;
/// <param name="TargetServings">When set, scale numeric ingredient qtys from recipe servings.</param>
public record PushRecipeIngredientsToQueueCommand(Guid RecipeId, int? TargetServings = null) : IRequest<int>;
public record ScaleRecipeQuery(Guid Id, int TargetServings) : IRequest<ScaledRecipeDto>;
public record ScaledRecipeDto(Guid Id, string Name, int BaseServings, int TargetServings, IReadOnlyList<string> Ingredients);

public class RecipeCommandHandlers :
    IRequestHandler<ListRecipesQuery, List<RecipeDto>>,
    IRequestHandler<GetRecipeQuery, RecipeDto>,
    IRequestHandler<CreateRecipeCommand, RecipeDto>,
    IRequestHandler<UpdateRecipeCommand, RecipeDto>,
    IRequestHandler<DeleteRecipeCommand>,
    IRequestHandler<ApplyRecipeToMealCommand, MealEntryDto>,
    IRequestHandler<PushRecipeIngredientsToQueueCommand, int>,
    IRequestHandler<ScaleRecipeQuery, ScaledRecipeDto>
{
    private readonly IFamilyOsDbContext _db;
    private readonly ICurrentUserService _current;

    public RecipeCommandHandlers(IFamilyOsDbContext db, ICurrentUserService current)
    {
        _db = db;
        _current = current;
    }

    public async Task<List<RecipeDto>> Handle(ListRecipesQuery query, CancellationToken ct)
    {
        await EnsureFamilyAsync(ct);
        var list = await _db.Recipes.AsNoTracking()
            .Where(r => r.FamilyId == _current.FamilyId)
            .OrderBy(r => r.Name)
            .ToListAsync(ct);
        return list.Select(ToDto).ToList();
    }

    public async Task<RecipeDto> Handle(GetRecipeQuery query, CancellationToken ct)
    {
        await EnsureFamilyAsync(ct);
        var r = await _db.Recipes.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == query.Id && x.FamilyId == _current.FamilyId, ct)
            ?? throw new NotFoundException("Recipe", query.Id);
        return ToDto(r);
    }

    public async Task<RecipeDto> Handle(CreateRecipeCommand cmd, CancellationToken ct)
    {
        await EnsureFamilyAsync(ct);
        var recipe = Recipe.Create(
            _current.FamilyId!.Value, cmd.Name, cmd.Servings, cmd.IngredientsCsv, cmd.Instructions);
        _db.Recipes.Add(recipe);
        await _db.SaveChangesAsync(ct);
        return ToDto(recipe);
    }

    public async Task<RecipeDto> Handle(UpdateRecipeCommand cmd, CancellationToken ct)
    {
        await EnsureFamilyAsync(ct);
        var recipe = await _db.Recipes
            .FirstOrDefaultAsync(x => x.Id == cmd.Id && x.FamilyId == _current.FamilyId, ct)
            ?? throw new NotFoundException("Recipe", cmd.Id);
        recipe.Update(cmd.Name, cmd.Servings, cmd.IngredientsCsv, cmd.Instructions);
        await _db.SaveChangesAsync(ct);
        return ToDto(recipe);
    }

    public async Task Handle(DeleteRecipeCommand cmd, CancellationToken ct)
    {
        await EnsureFamilyAsync(ct);
        var recipe = await _db.Recipes
            .FirstOrDefaultAsync(x => x.Id == cmd.Id && x.FamilyId == _current.FamilyId, ct)
            ?? throw new NotFoundException("Recipe", cmd.Id);
        recipe.SoftDelete();
        await _db.SaveChangesAsync(ct);
    }

    public async Task<MealEntryDto> Handle(ApplyRecipeToMealCommand cmd, CancellationToken ct)
    {
        await EnsureFamilyAsync(ct);
        if (!Enum.TryParse<MealSlot>(cmd.Slot, ignoreCase: true, out var slot))
            throw new ValidationException("Slot", "Slot must be Breakfast, Lunch, Dinner, or Snack.");

        var recipe = await _db.Recipes.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == cmd.RecipeId && x.FamilyId == _current.FamilyId, ct)
            ?? throw new NotFoundException("Recipe", cmd.RecipeId);

        var week = MealPlan.ToMonday(cmd.WeekStartUtc ?? cmd.DayUtc);
        var plan = await _db.MealPlans
            .Include(p => p.Entries)
            .FirstOrDefaultAsync(p => p.FamilyId == _current.FamilyId && p.WeekStartUtc == week, ct);
        if (plan is null)
        {
            plan = MealPlan.Create(_current.FamilyId!.Value, week);
            _db.MealPlans.Add(plan);
            await _db.SaveChangesAsync(ct);
        }

        var entry = plan.UpsertEntry(
            cmd.DayUtc, slot, recipe.Name, notes: $"{recipe.Servings} servings", recipe.IngredientsCsv);
        await _db.SaveChangesAsync(ct);
        return new MealEntryDto(
            entry.Id, entry.DayUtc, entry.Slot.ToString(), entry.DishName, entry.Notes, entry.IngredientsCsv);
    }

    public async Task<int> Handle(PushRecipeIngredientsToQueueCommand cmd, CancellationToken ct)
    {
        await EnsureFamilyAsync(ct);
        var recipe = await _db.Recipes.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == cmd.RecipeId && x.FamilyId == _current.FamilyId, ct)
            ?? throw new NotFoundException("Recipe", cmd.RecipeId);

        var names = cmd.TargetServings is int target && target != recipe.Servings
            ? recipe.ScaleIngredientsForServings(target)
            : recipe.IngredientList();
        if (names.Count == 0) return 0;

        var existing = await _db.ProcurementItems
            .Where(i => i.FamilyId == _current.FamilyId
                && (i.Status == ProcurementItemStatus.Queued || i.Status == ProcurementItemStatus.InCart))
            .Select(i => i.Name)
            .ToListAsync(ct);
        var existingSet = new HashSet<string>(existing, StringComparer.OrdinalIgnoreCase);

        var added = 0;
        var approver = _current.MemberId!.Value;
        foreach (var name in names)
        {
            if (existingSet.Contains(name)) continue;
            _db.ProcurementItems.Add(ProcurementItem.Create(
                _current.FamilyId!.Value,
                name,
                approver,
                category: "Recipe"));
            existingSet.Add(name);
            added++;
        }

        if (added > 0)
            await _db.SaveChangesAsync(ct);
        return added;
    }

    public async Task<ScaledRecipeDto> Handle(ScaleRecipeQuery query, CancellationToken ct)
    {
        await EnsureFamilyAsync(ct);
        var recipe = await _db.Recipes.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == query.Id && x.FamilyId == _current.FamilyId, ct)
            ?? throw new NotFoundException("Recipe", query.Id);
        var ingredients = recipe.ScaleIngredientsForServings(query.TargetServings);
        return new ScaledRecipeDto(recipe.Id, recipe.Name, recipe.Servings, query.TargetServings, ingredients);
    }

    private async Task EnsureFamilyAsync(CancellationToken ct)
    {
        await _current.EnsureLoadedAsync(ct);
        if (!_current.IsAuthenticated || _current.FamilyId is null)
            throw new UnauthorizedException();
    }

    private static RecipeDto ToDto(Recipe r) =>
        new(r.Id, r.Name, r.Servings, r.IngredientsCsv, r.Instructions);
}
