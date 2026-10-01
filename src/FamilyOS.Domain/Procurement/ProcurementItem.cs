using FamilyOS.Domain.Common;

namespace FamilyOS.Domain.Procurement;

public class ProcurementItem : Entity
{
    public Guid FamilyId { get; private set; }
    public Guid? RequestId { get; private set; }
    public Guid? ProductId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Brand { get; private set; }
    public string? Size { get; private set; }
    public string? Category { get; private set; }
    public decimal? Quantity { get; private set; }
    public string? Unit { get; private set; }
    public decimal? EstimatedPrice { get; private set; }
    public decimal? ActualPrice { get; private set; }
    public string? PreferredStore { get; private set; }
    public ProcurementItemStatus Status { get; private set; } = ProcurementItemStatus.Approved;
    public Guid? CartId { get; private set; }
    public Guid ApprovedByMemberId { get; private set; }
    public DateTime? PurchasedAtUtc { get; private set; }
    public string? Notes { get; private set; }

    private ProcurementItem() { }

    public static ProcurementItem Create(
        Guid familyId,
        string name,
        Guid approvedByMemberId,
        Guid? requestId = null,
        string? brand = null,
        string? size = null,
        string? category = null,
        decimal? quantity = 1,
        decimal? estimatedPrice = null,
        string? preferredStore = null)
    {
        return new ProcurementItem
        {
            FamilyId = familyId,
            Name = name.Trim(),
            ApprovedByMemberId = approvedByMemberId,
            RequestId = requestId,
            Brand = brand,
            Size = size,
            Category = category,
            Quantity = quantity,
            EstimatedPrice = estimatedPrice,
            PreferredStore = preferredStore,
            Status = ProcurementItemStatus.Queued
        };
    }

    public void AddToCart(Guid cartId)
    {
        CartId = cartId;
        Status = ProcurementItemStatus.InCart;
        MarkUpdated();
    }

    public void Hold()
    {
        Status = ProcurementItemStatus.Held;
        CartId = null;
        MarkUpdated();
    }

    public void Defer()
    {
        Status = ProcurementItemStatus.Deferred;
        CartId = null;
        MarkUpdated();
    }

    public void MarkPurchased(decimal actualPrice, Guid? productId = null)
    {
        ActualPrice = actualPrice;
        PurchasedAtUtc = DateTime.UtcNow;
        Status = ProcurementItemStatus.Purchased;
        if (productId.HasValue) ProductId = productId;
        MarkUpdated();
    }

    public void MarkNoLongerNeeded()
    {
        Status = ProcurementItemStatus.NoLongerNeeded;
        MarkUpdated();
    }
}

public class Product : Entity
{
    public Guid FamilyId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Brand { get; private set; }
    public string? Size { get; private set; }
    public string? Category { get; private set; }
    public decimal? TypicalPrice { get; private set; }
    public string? TypicalStores { get; private set; }
    public int PurchaseCount { get; private set; }
    public DateTime? LastPurchasedAtUtc { get; private set; }

    private Product() { }

    public static Product Create(Guid familyId, string name, string? brand = null, string? size = null, string? category = null)
    {
        return new Product
        {
            FamilyId = familyId,
            Name = name.Trim(),
            Brand = brand,
            Size = size,
            Category = category
        };
    }

    public void RecordPurchase(decimal price, string? store)
    {
        PurchaseCount++;
        LastPurchasedAtUtc = DateTime.UtcNow;
        TypicalPrice = TypicalPrice.HasValue
            ? Math.Round((TypicalPrice.Value * (PurchaseCount - 1) + price) / PurchaseCount, 2)
            : price;
        if (!string.IsNullOrEmpty(store))
        {
            var stores = (TypicalStores ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
            if (!stores.Contains(store, StringComparer.OrdinalIgnoreCase))
            {
                stores.Add(store);
                TypicalStores = string.Join(", ", stores);
            }
        }
        MarkUpdated();
    }
}

public class Cart : Entity
{
    public Guid FamilyId { get; private set; }
    public string StoreName { get; private set; } = string.Empty;
    public Guid? OwnerMemberId { get; private set; }
    public bool IsActive { get; private set; } = true;
    public DateTime? ShoppingTripDate { get; private set; }

    private readonly List<Guid> _itemIds = new();
    public IReadOnlyCollection<Guid> ItemIds => _itemIds.AsReadOnly();

    private Cart() { }

    public static Cart Create(Guid familyId, string storeName, Guid? ownerMemberId = null)
    {
        return new Cart
        {
            FamilyId = familyId,
            StoreName = storeName.Trim(),
            OwnerMemberId = ownerMemberId
        };
    }

    public void AddItem(Guid procurementItemId)
    {
        if (!_itemIds.Contains(procurementItemId))
            _itemIds.Add(procurementItemId);
        MarkUpdated();
    }

    public void RemoveItem(Guid procurementItemId)
    {
        _itemIds.Remove(procurementItemId);
        MarkUpdated();
    }

    public void Close()
    {
        IsActive = false;
        MarkUpdated();
    }
}
