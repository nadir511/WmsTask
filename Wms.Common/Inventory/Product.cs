using System.IO.Compression;

namespace Wms.Outbound.Inventory;

public class Product
{
    public string ProductId { get; set; } = default!;
    public string ProductName { get; set; } = default!;
}
