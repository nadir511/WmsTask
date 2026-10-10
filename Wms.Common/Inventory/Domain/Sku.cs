namespace Wms.Outbound.Inventory.Domain;

public class Sku
{
    public string SkuId { get; set; } = default!;
    public int ProductQuantity { get; set; } = default!;
    public Product Product { get; set; } = default!;
    public WarehouseLocation Location { get; set; } = default!;
}
