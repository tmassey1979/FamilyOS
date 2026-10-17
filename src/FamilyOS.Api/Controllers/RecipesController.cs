using FamilyOS.Application.Meals;
using FamilyOS.Infrastructure.Identity;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FamilyOS.Api.Controllers;

[ApiController]
[Route("api/recipes")]
[Authorize(Policy = FamilyAuthPolicies.FamilyMember)]
public class RecipesController : ControllerBase
{
    private readonly IMediator _mediator;

    public RecipesController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    public async Task<ActionResult<List<RecipeDto>>> List(CancellationToken ct)
        => Ok(await _mediator.Send(new ListRecipesQuery(), ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<RecipeDto>> Get(Guid id, CancellationToken ct)
        => Ok(await _mediator.Send(new GetRecipeQuery(id), ct));

    [HttpGet("{id:guid}/scale")]
    public async Task<ActionResult<ScaledRecipeDto>> Scale(Guid id, [FromQuery] int servings, CancellationToken ct)
        => Ok(await _mediator.Send(new ScaleRecipeQuery(id, servings), ct));

    [HttpPost]
    public async Task<ActionResult<RecipeDto>> Create([FromBody] RecipeBody body, CancellationToken ct)
        => Ok(await _mediator.Send(new CreateRecipeCommand(
            body.Name, body.Servings ?? 4, body.IngredientsCsv, body.Instructions), ct));

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<RecipeDto>> Update(Guid id, [FromBody] RecipeBody body, CancellationToken ct)
        => Ok(await _mediator.Send(new UpdateRecipeCommand(
            id, body.Name, body.Servings ?? 4, body.IngredientsCsv, body.Instructions), ct));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _mediator.Send(new DeleteRecipeCommand(id), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/apply")]
    public async Task<ActionResult<MealEntryDto>> Apply(Guid id, [FromBody] ApplyRecipeBody body, CancellationToken ct)
        => Ok(await _mediator.Send(new ApplyRecipeToMealCommand(
            id, body.DayUtc, body.Slot, body.WeekStartUtc), ct));

    [HttpPost("{id:guid}/to-shopping")]
    public async Task<ActionResult<object>> ToShopping(
        Guid id, [FromBody] ToShoppingBody? body, CancellationToken ct)
    {
        var added = await _mediator.Send(
            new PushRecipeIngredientsToQueueCommand(id, body?.TargetServings), ct);
        return Ok(new { added, targetServings = body?.TargetServings });
    }

    public record RecipeBody(
        string Name,
        int? Servings = 4,
        string? IngredientsCsv = null,
        string? Instructions = null);

    public record ApplyRecipeBody(
        DateTime DayUtc,
        string Slot,
        DateTime? WeekStartUtc = null);

    public record ToShoppingBody(int? TargetServings = null);
}
