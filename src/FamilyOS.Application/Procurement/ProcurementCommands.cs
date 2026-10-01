using FamilyOS.Application.Common;
using FamilyOS.Application.Interfaces;
using FamilyOS.Domain.Common;
using FamilyOS.Domain.Procurement;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FamilyOS.Application.Procurement;

public record ProcurementItemDto(
    Guid Id, string Name, string? Brand, string? Size, string? Category,
    decimal? Quantity, decimal? EstimatedPrice, decimal? ActualPrice,
    string? PreferredStore, ProcurementItemStatus Status, DateTime CreatedAtUtc);

public record CartDto(Guid Id, string StoreName, List<ProcurementItemDto> Items, bool IsActive);

public record GetProcurementQueueQuery() : IRequest<List<ProcurementItemDto>>;
public record GetCartsQuery() : IRequest<List<CartDto>>;
public record AddToCartCommand(Guid ProcurementItemId, Guid CartId) : IRequest<ProcurementItemDto>;
public record CreateCartCommand(string StoreName) : IRequest<CartDto>;
public record HoldItemCommand(Guid ProcurementItemId) : IRequest<ProcurementItemDto>;
public record MarkPurchasedCommand(Guid ProcurementItemId, decimal ActualPrice, string? Store) : IRequest<ProcurementItemDto>;
public record GetProductHistoryQuery() : IRequest<List<ProductDto>>;
public record ProductDto(Guid Id, string Name, string? Brand, string? Size, string? Category, decimal? TypicalPrice, int PurchaseCount, DateTime? LastPurchasedAtUtc);

public class ProcurementHandlers :
    IRequestHandler<GetProcurementQueueQuery, List<ProcurementItemDto>>,
    IRequestHandler<GetCartsQuery, List<CartDto>>,
    IRequestHandler<AddToCartCommand, ProcurementItemDto>,
    IRequestHandler<CreateCartCommand, CartDto>,
    IRequestHandler<HoldItemCommand, ProcurementItemDto>,
    IRequestHandler<MarkPurchasedCommand, ProcurementItemDto>,
    IRequestHandler<GetProductHistoryQuery, List<ProductDto>>
{
    private readonly IFamilyOsDbContext _db;
    private readonly ICurrentUserService _current;

    public ProcurementHandlers(IFamilyOsDbContext db, ICurrentUserService current)
    {
        _db = db;
        _current = current;
    }

    public async Task<List<ProcurementItemDto>> Handle(GetProcurementQueueQuery query, CancellationToken ct)
    {
        await _current.EnsureLoadedAsync(ct);
        EnsureAuth();

        var items = await _db.ProcurementItems.AsNoTracking()
            .Where(p => p.FamilyId == _current.FamilyId &&
                        (p.Status == ProcurementItemStatus.Queued ||
                         p.Status == ProcurementItemStatus.Approved ||
                         p.Status == ProcurementItemStatus.Held ||
                         p.Status == ProcurementItemStatus.Deferred))
            .OrderBy(p => p.CreatedAtUtc)
            .ToListAsync(ct);

        return items.Select(ToDto).ToList();
    }

    public async Task<List<CartDto>> Handle(GetCartsQuery query, CancellationToken ct)
    {
        await _current.EnsureLoadedAsync(ct);
        EnsureAuth();

        var carts = await _db.Carts.AsNoTracking()
            .Where(c => c.FamilyId == _current.FamilyId && c.IsActive)
            .ToListAsync(ct);

        var result = new List<CartDto>();
        foreach (var cart in carts)
        {
            var items = await _db.ProcurementItems.AsNoTracking()
                .Where(p => p.CartId == cart.Id && p.Status == ProcurementItemStatus.InCart)
                .ToListAsync(ct);
            result.Add(new CartDto(cart.Id, cart.StoreName, items.Select(ToDto).ToList(), cart.IsActive));
        }
        return result;
    }

    public async Task<ProcurementItemDto> Handle(AddToCartCommand cmd, CancellationToken ct)
    {
        await _current.EnsureLoadedAsync(ct);
        EnsureAdult();

        var item = await _db.ProcurementItems
            .FirstOrDefaultAsync(p => p.Id == cmd.ProcurementItemId && p.FamilyId == _current.FamilyId, ct)
            ?? throw new NotFoundException("ProcurementItem", cmd.ProcurementItemId);

        var cart = await _db.Carts
            .FirstOrDefaultAsync(c => c.Id == cmd.CartId && c.FamilyId == _current.FamilyId, ct)
            ?? throw new NotFoundException("Cart", cmd.CartId);

        item.AddToCart(cart.Id);
        cart.AddItem(item.Id);
        await _db.SaveChangesAsync(ct);
        return ToDto(item);
    }

    public async Task<CartDto> Handle(CreateCartCommand cmd, CancellationToken ct)
    {
        await _current.EnsureLoadedAsync(ct);
        EnsureAdult();

        var cart = Cart.Create(_current.FamilyId!.Value, cmd.StoreName, _current.MemberId);
        _db.Carts.Add(cart);
        await _db.SaveChangesAsync(ct);
        return new CartDto(cart.Id, cart.StoreName, new(), cart.IsActive);
    }

    public async Task<ProcurementItemDto> Handle(HoldItemCommand cmd, CancellationToken ct)
    {
        await _current.EnsureLoadedAsync(ct);
        EnsureAdult();

        var item = await _db.ProcurementItems
            .FirstOrDefaultAsync(p => p.Id == cmd.ProcurementItemId && p.FamilyId == _current.FamilyId, ct)
            ?? throw new NotFoundException("ProcurementItem", cmd.ProcurementItemId);

        item.Hold();
        await _db.SaveChangesAsync(ct);
        return ToDto(item);
    }

    public async Task<ProcurementItemDto> Handle(MarkPurchasedCommand cmd, CancellationToken ct)
    {
        await _current.EnsureLoadedAsync(ct);
        EnsureAdult();

        var item = await _db.ProcurementItems
            .FirstOrDefaultAsync(p => p.Id == cmd.ProcurementItemId && p.FamilyId == _current.FamilyId, ct)
            ?? throw new NotFoundException("ProcurementItem", cmd.ProcurementItemId);

        var product = await _db.Products
            .FirstOrDefaultAsync(p => p.FamilyId == _current.FamilyId &&
                p.Name.ToLower() == item.Name.ToLower() &&
                (item.Brand == null || p.Brand == item.Brand), ct);

        if (product == null)
        {
            product = Product.Create(_current.FamilyId!.Value, item.Name, item.Brand, item.Size, item.Category);
            _db.Products.Add(product);
            await _db.SaveChangesAsync(ct);
        }

        product.RecordPurchase(cmd.ActualPrice, cmd.Store);
        item.MarkPurchased(cmd.ActualPrice, product.Id);
        await _db.SaveChangesAsync(ct);
        return ToDto(item);
    }

    public async Task<List<ProductDto>> Handle(GetProductHistoryQuery query, CancellationToken ct)
    {
        await _current.EnsureLoadedAsync(ct);
        EnsureAuth();

        return await _db.Products.AsNoTracking()
            .Where(p => p.FamilyId == _current.FamilyId)
            .OrderByDescending(p => p.LastPurchasedAtUtc)
            .Select(p => new ProductDto(p.Id, p.Name, p.Brand, p.Size, p.Category, p.TypicalPrice, p.PurchaseCount, p.LastPurchasedAtUtc))
            .ToListAsync(ct);
    }

    private void EnsureAuth()
    {
        if (!_current.IsAuthenticated || !_current.FamilyId.HasValue)
            throw new ForbiddenException("Not authenticated.");
    }

    private void EnsureAdult()
    {
        EnsureAuth();
        if (_current.Role is not (FamilyRole.Owner or FamilyRole.Adult))
            throw new ForbiddenException("Only Adults and Owners can manage procurement.");
    }

    private static ProcurementItemDto ToDto(ProcurementItem p) =>
        new(p.Id, p.Name, p.Brand, p.Size, p.Category, p.Quantity,
            p.EstimatedPrice, p.ActualPrice, p.PreferredStore, p.Status, p.CreatedAtUtc);
}
