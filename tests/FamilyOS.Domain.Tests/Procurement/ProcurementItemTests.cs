using FamilyOS.Domain.Common;
using FamilyOS.Domain.Procurement;
using FluentAssertions;

namespace FamilyOS.Domain.Tests.Procurement;

public class ProcurementItemTests
{
    private static readonly Guid FamilyId = Guid.NewGuid();
    private static readonly Guid ApproverId = Guid.NewGuid();
    private static readonly Guid CartId = Guid.NewGuid();

    private static ProcurementItem NewItem(string name = "Milk") =>
        ProcurementItem.Create(FamilyId, name, ApproverId, quantity: 1, estimatedPrice: 3.50m, preferredStore: "Kroger");

    [Fact]
    public void Create_starts_Queued()
    {
        var item = NewItem();
        item.Status.Should().Be(ProcurementItemStatus.Queued);
        item.Name.Should().Be("Milk");
        item.PreferredStore.Should().Be("Kroger");
        item.CartId.Should().BeNull();
    }

    [Fact]
    public void AddToCart_sets_InCart_and_CartId()
    {
        var item = NewItem();
        item.AddToCart(CartId);
        item.Status.Should().Be(ProcurementItemStatus.InCart);
        item.CartId.Should().Be(CartId);
    }

    [Fact]
    public void Hold_clears_cart_and_sets_Held()
    {
        var item = NewItem();
        item.AddToCart(CartId);
        item.Hold();
        item.Status.Should().Be(ProcurementItemStatus.Held);
        item.CartId.Should().BeNull();
    }

    [Fact]
    public void Defer_sets_Deferred()
    {
        var item = NewItem();
        item.Defer();
        item.Status.Should().Be(ProcurementItemStatus.Deferred);
        item.CartId.Should().BeNull();
    }

    [Fact]
    public void MarkNoLongerNeeded_sets_status_and_clears_cart()
    {
        var item = NewItem();
        item.AddToCart(CartId);
        item.MarkNoLongerNeeded();
        item.Status.Should().Be(ProcurementItemStatus.NoLongerNeeded);
        item.CartId.Should().BeNull();
    }

    [Fact]
    public void RemoveFromCart_returns_to_Queued()
    {
        var item = NewItem();
        item.AddToCart(CartId);
        item.RemoveFromCart();
        item.Status.Should().Be(ProcurementItemStatus.Queued);
        item.CartId.Should().BeNull();
    }

    [Fact]
    public void ReturnToQueue_from_Held()
    {
        var item = NewItem();
        item.Hold();
        item.ReturnToQueue();
        item.Status.Should().Be(ProcurementItemStatus.Queued);
    }

    [Fact]
    public void MarkPurchased_records_price()
    {
        var item = NewItem();
        item.AddToCart(CartId);
        var productId = Guid.NewGuid();
        item.MarkPurchased(4.25m, productId);
        item.Status.Should().Be(ProcurementItemStatus.Purchased);
        item.ActualPrice.Should().Be(4.25m);
        item.PurchasedAtUtc.Should().NotBeNull();
        item.ProductId.Should().Be(productId);
    }
}

public class CartTests
{
    private static readonly Guid FamilyId = Guid.NewGuid();
    private static readonly Guid OwnerId = Guid.NewGuid();

    [Fact]
    public void Create_is_active()
    {
        var cart = Cart.Create(FamilyId, "Costco", OwnerId);
        cart.IsActive.Should().BeTrue();
        cart.StoreName.Should().Be("Costco");
        cart.ItemIds.Should().BeEmpty();
    }

    [Fact]
    public void AddItem_is_idempotent()
    {
        var cart = Cart.Create(FamilyId, "Target");
        var id = Guid.NewGuid();
        cart.AddItem(id);
        cart.AddItem(id);
        cart.ItemIds.Should().ContainSingle().Which.Should().Be(id);
    }

    [Fact]
    public void Abandon_clears_items_and_deactivates()
    {
        var cart = Cart.Create(FamilyId, "Walmart");
        cart.AddItem(Guid.NewGuid());
        cart.Abandon();
        cart.IsActive.Should().BeFalse();
        cart.ItemIds.Should().BeEmpty();
    }
}
