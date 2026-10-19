using FamilyOS.Domain.Procurement;
using FluentAssertions;

namespace FamilyOS.Domain.Tests.Procurement;

public class ProcurementDomainTests
{
    private static readonly Guid FamilyId = Guid.NewGuid();
    private static readonly Guid Approver = Guid.NewGuid();

    [Fact]
    public void Create_queues_item()
    {
        var item = ProcurementItem.Create(FamilyId, "Milk", Approver, quantity: 2, estimatedPrice: 3.5m, preferredStore: "Costco");
        item.Status.Should().Be(ProcurementItemStatus.Queued);
        item.Name.Should().Be("Milk");
        item.Quantity.Should().Be(2);
        item.PreferredStore.Should().Be("Costco");
    }

    [Fact]
    public void Cart_flow_add_purchase_remove()
    {
        var item = ProcurementItem.Create(FamilyId, "Eggs", Approver);
        var cart = Cart.Create(FamilyId, "Kroger", Approver);

        item.AddToCart(cart.Id);
        cart.AddItem(item.Id);
        item.Status.Should().Be(ProcurementItemStatus.InCart);
        cart.ItemIds.Should().Contain(item.Id);

        item.MarkPurchased(4.99m);
        item.Status.Should().Be(ProcurementItemStatus.Purchased);
        item.ActualPrice.Should().Be(4.99m);
        item.PurchasedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public void Hold_Defer_NLN_ReturnToQueue()
    {
        var item = ProcurementItem.Create(FamilyId, "Chips", Approver);
        var cartId = Guid.NewGuid();
        item.AddToCart(cartId);

        item.Hold();
        item.Status.Should().Be(ProcurementItemStatus.Held);
        item.CartId.Should().BeNull();

        item.ReturnToQueue();
        item.Status.Should().Be(ProcurementItemStatus.Queued);

        item.Defer();
        item.Status.Should().Be(ProcurementItemStatus.Deferred);

        item.ReturnToQueue();
        item.MarkNoLongerNeeded();
        item.Status.Should().Be(ProcurementItemStatus.NoLongerNeeded);
    }

    [Fact]
    public void RemoveFromCart_only_when_in_cart()
    {
        var item = ProcurementItem.Create(FamilyId, "Bread", Approver);
        item.RemoveFromCart(); // no-op while queued
        item.Status.Should().Be(ProcurementItemStatus.Queued);

        item.AddToCart(Guid.NewGuid());
        item.RemoveFromCart();
        item.Status.Should().Be(ProcurementItemStatus.Queued);
        item.CartId.Should().BeNull();
    }

    [Fact]
    public void Cart_abandon_clears_items_and_closes()
    {
        var cart = Cart.Create(FamilyId, "Target");
        cart.AddItem(Guid.NewGuid());
        cart.AddItem(Guid.NewGuid());
        cart.Abandon();
        cart.IsActive.Should().BeFalse();
        cart.ItemIds.Should().BeEmpty();
    }

    [Fact]
    public void Product_RecordPurchase_averages_price()
    {
        var p = Product.Create(FamilyId, "Oat milk", brand: "Oatly", category: "Dairy");
        p.RecordPurchase(5m, "Whole Foods");
        p.PurchaseCount.Should().Be(1);
        p.TypicalPrice.Should().Be(5m);

        p.RecordPurchase(7m, "Costco");
        p.PurchaseCount.Should().Be(2);
        p.TypicalPrice.Should().Be(6m);
        p.TypicalStores.Should().Contain("Whole Foods");
        p.TypicalStores.Should().Contain("Costco");
    }
}
