using FamilyOS.Application.Common;
using FamilyOS.Application.Interfaces;
using FamilyOS.Domain.Meals;
using FamilyOS.Domain.Procurement;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FamilyOS.Application.Meals;

public record MealEntryDto(
    Guid Id, DateTime DayUtc, string Slot, string DishName, string? Notes, string? IngredientsCsv);

public record MealPlanDto(
    Guid Id, DateTime WeekStartUtc, string? Notes, List<MealEntryDto> Entries);

public record GetMealPlanWeekQuery(DateTime? WeekStartUtc = null) : IRequest<MealPlanDto>;

public record UpsertMealEntryCommand(
    DateTime DayUtc,
    string Slot,
    string DishName,
    string? Notes = null,
    string? IngredientsCsv = null,
    DateTime? WeekStartUtc = null
) : IRequest<MealEntryDto>;

public record RemoveMealEntryCommand(Guid EntryId) : IRequest;

public record PushMealIngredientsToQueueCommand(DateTime? WeekStartUtc = null) : IRequest<int>;

public class MealCommandHandlers :
    IRequestHandler<GetMealPlanWeekQuery, MealPlanDto>,
    IRequestHandler<UpsertMealEntryCommand, MealEntryDto>,
    IRequestHandler<RemoveMealEntryCommand>,
    IRequestHandler<PushMealIngredientsToQueueCommand, int>
{
    private readonly IFamilyOsDbContext _db;
    private readonly ICurrentUserService _current;

    public MealCommandHandlers(IFamilyOsDbContext db, ICurrentUserService current)
    {
        _db = db;
        _current = current;
    }

    public async Task<MealPlanDto> Handle(GetMealPlanWeekQuery query, CancellationToken ct)
    {
        await _current.EnsureLoadedAsync(ct);
        EnsureAuth();
        var week = MealPlan.ToMonday(query.WeekStartUtc ?? DateTime.UtcNow);
        var plan = await LoadOrCreateAsync(week, ct);
        return ToDto(plan);
    }

    public async Task<MealEntryDto> Handle(UpsertMealEntryCommand cmd, CancellationToken ct)
    {
        await _current.EnsureLoadedAsync(ct);
        EnsureAuth();
        if (!Enum.TryParse<MealSlot>(cmd.Slot, ignoreCase: true, out var slot))
            throw new ValidationException("Slot must be Breakfast, Lunch, Dinner, or Snack.");

        var week = MealPlan.ToMonday(cmd.WeekStartUtc ?? cmd.DayUtc);
        var plan = await LoadOrCreateAsync(week, ct);
        var entry = plan.UpsertEntry(cmd.DayUtc, slot, cmd.DishName, cmd.Notes, cmd.IngredientsCsv);
        await _db.SaveChangesAsync(ct);
        return ToEntryDto(entry);
    }

    public async Task Handle(RemoveMealEntryCommand cmd, CancellationToken ct)
    {
        await _current.EnsureLoadedAsync(ct);
        EnsureAuth();
        var entry = await _db.MealEntries
            .FirstOrDefaultAsync(e => e.Id == cmd.EntryId && e.FamilyId == _current.FamilyId, ct)
            ?? throw new NotFoundException("MealEntry", cmd.EntryId);
        var plan = await _db.MealPlans
            .Include(p => p.Entries)
            .FirstOrDefaultAsync(p => p.Id == entry.MealPlanId, ct)
            ?? throw new NotFoundException("MealPlan", entry.MealPlanId);
        plan.RemoveEntry(cmd.EntryId);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<int> Handle(PushMealIngredientsToQueueCommand cmd, CancellationToken ct)
    {
        await _current.EnsureLoadedAsync(ct);
        EnsureAuth();
        var week = MealPlan.ToMonday(cmd.WeekStartUtc ?? DateTime.UtcNow);
        var plan = await _db.MealPlans
            .AsNoTracking()
            .Include(p => p.Entries)
            .FirstOrDefaultAsync(p => p.FamilyId == _current.FamilyId && p.WeekStartUtc == week, ct);
        if (plan is null) return 0;

        var names = plan.Entries
            .Where(e => !e.IsDeleted)
            .SelectMany(e => e.IngredientList())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (names.Count == 0) return 0;

        var existing = await _db.ProcurementItems
            .Where(i => i.FamilyId == _current.FamilyId
                && (i.Status == ProcurementItemStatus.Queued || i.Status == ProcurementItemStatus.InCart))
            .Select(i => i.Name)
            .ToListAsync(ct);
        var existingSet = new HashSet<string>(existing, StringComparer.OrdinalIgnoreCase);

        var added = 0;
        foreach (var name in names)
        {
            if (existingSet.Contains(name)) continue;
            _db.ProcurementItems.Add(ProcurementItem.Create(
                _current.FamilyId!.Value,
                name,
                category: "Meal plan",
                preferredStore: null));
            existingSet.Add(name);
            added++;
        }

        if (added > 0)
            await _db.SaveChangesAsync(ct);
        return added;
    }

    private async Task<MealPlan> LoadOrCreateAsync(DateTime weekStart, CancellationToken ct)
    {
        var plan = await _db.MealPlans
            .Include(p => p.Entries)
            .FirstOrDefaultAsync(p => p.FamilyId == _current.FamilyId && p.WeekStartUtc == weekStart, ct);
        if (plan is not null) return plan;

        plan = MealPlan.Create(_current.FamilyId!.Value, weekStart);
        _db.MealPlans.Add(plan);
        await _db.SaveChangesAsync(ct);
        return plan;
    }

    private void EnsureAuth()
    {
        if (!_current.IsAuthenticated || _current.FamilyId is null)
            throw new UnauthorizedException();
    }

    private static MealPlanDto ToDto(MealPlan plan) => new(
        plan.Id,
        plan.WeekStartUtc,
        plan.Notes,
        plan.Entries.Where(e => !e.IsDeleted)
            .OrderBy(e => e.DayUtc)
            .ThenBy(e => e.Slot)
            .Select(ToEntryDto)
            .ToList());

    private static MealEntryDto ToEntryDto(MealEntry e) => new(
        e.Id, e.DayUtc, e.Slot.ToString(), e.DishName, e.Notes, e.IngredientsCsv);
}
