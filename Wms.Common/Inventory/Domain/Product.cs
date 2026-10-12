namespace Wms.Outbound.Inventory.Domain;

public class Product
{
    private int _productId;
    public string ProductId { get => $"P-{_productId}"; set => _productId = int.Parse(value); }
    public string ProductName { get; set; } = default!;
}
