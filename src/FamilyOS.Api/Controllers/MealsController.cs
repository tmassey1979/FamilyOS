using FamilyOS.Application.Meals;
using FamilyOS.Infrastructure.Identity;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FamilyOS.Api.Controllers;

[ApiController]
[Route("api/meals")]
[Authorize(Policy = FamilyAuthPolicies.FamilyMember)]
public class MealsController : ControllerBase
{
    private readonly IMediator _mediator;

    public MealsController(IMediator mediator) => _mediator = mediator;

    [HttpGet("week")]
    public async Task<ActionResult<MealPlanDto>> GetWeek([FromQuery] DateTime? weekStart, CancellationToken ct)
        => Ok(await _mediator.Send(new GetMealPlanWeekQuery(weekStart), ct));

    [HttpPut("entries")]
    public async Task<ActionResult<MealEntryDto>> UpsertEntry([FromBody] UpsertMealBody body, CancellationToken ct)
        => Ok(await _mediator.Send(new UpsertMealEntryCommand(
            body.DayUtc, body.Slot, body.DishName, body.Notes, body.IngredientsCsv, body.WeekStartUtc), ct));

    [HttpDelete("entries/{id:guid}")]
    public async Task<IActionResult> RemoveEntry(Guid id, CancellationToken ct)
    {
        await _mediator.Send(new RemoveMealEntryCommand(id), ct);
        return NoContent();
    }

    [HttpPost("week/to-shopping")]
    public async Task<ActionResult<object>> ToShopping([FromQuery] DateTime? weekStart, CancellationToken ct)
    {
        var added = await _mediator.Send(new PushMealIngredientsToQueueCommand(weekStart), ct);
        return Ok(new { added });
    }

    public record UpsertMealBody(
        DateTime DayUtc,
        string Slot,
        string DishName,
        string? Notes = null,
        string? IngredientsCsv = null,
        DateTime? WeekStartUtc = null);
}
