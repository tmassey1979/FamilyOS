using FamilyOS.Application.Procurement;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FamilyOS.Api.Controllers;

[ApiController]
[Route("api/procurement")]
[Authorize]
public class ProcurementController : ControllerBase
{
    private readonly IMediator _mediator;

    public ProcurementController(IMediator mediator) => _mediator = mediator;

    [HttpGet("queue")]
    public async Task<ActionResult<List<ProcurementItemDto>>> Queue(CancellationToken ct)
        => Ok(await _mediator.Send(new GetProcurementQueueQuery(), ct));

    [HttpGet("carts")]
    public async Task<ActionResult<List<CartDto>>> Carts(CancellationToken ct)
        => Ok(await _mediator.Send(new GetCartsQuery(), ct));

    [HttpPost("carts")]
    public async Task<ActionResult<CartDto>> CreateCart([FromBody] CreateCartCommand cmd, CancellationToken ct)
        => Ok(await _mediator.Send(cmd, ct));

    [HttpPost("items/{id:guid}/add-to-cart")]
    public async Task<ActionResult<ProcurementItemDto>> AddToCart(Guid id, [FromBody] CartBody body, CancellationToken ct)
        => Ok(await _mediator.Send(new AddToCartCommand(id, body.CartId), ct));

    [HttpPost("items/{id:guid}/hold")]
    public async Task<ActionResult<ProcurementItemDto>> Hold(Guid id, CancellationToken ct)
        => Ok(await _mediator.Send(new HoldItemCommand(id), ct));

    [HttpPost("items/{id:guid}/purchase")]
    public async Task<ActionResult<ProcurementItemDto>> Purchase(Guid id, [FromBody] PurchaseBody body, CancellationToken ct)
        => Ok(await _mediator.Send(new MarkPurchasedCommand(id, body.ActualPrice, body.Store), ct));

    [HttpGet("products")]
    public async Task<ActionResult<List<ProductDto>>> Products(CancellationToken ct)
        => Ok(await _mediator.Send(new GetProductHistoryQuery(), ct));

    public record CartBody(Guid CartId);
    public record PurchaseBody(decimal ActualPrice, string? Store);
}
