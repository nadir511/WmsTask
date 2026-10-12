namespace Wms.Outbound.Inventory.Domain;

public class Sku
{
    public Sku(string skuId, int productQuantity, Product product, WarehouseLocation location)
    {
        SkuId = skuId;
        ProductQuantity = productQuantity;
        Product = product;
        Location = location;
    }

    public string SkuId { get; }
    public int ProductQuantity { get; private set; }
    public Product Product { get; }
    public WarehouseLocation Location { get; }

    public void Reserve(int quantity)
    {
        if (quantity > ProductQuantity)
            throw new InvalidOperationException($"Cannot reserve {quantity} units of {SkuId}; only {ProductQuantity} available.");

        ProductQuantity -= quantity;
    }

    public void Release(int quantity)
    {
        ProductQuantity += quantity;
    }
}
