namespace Wms.Outbound.Inventory.Domain;

public class WarehouseLocation
{
    public string Id { get; set; } = default!;
    public bool IsLocked { get; set; }

    public bool CanAllocateStock()
    {
        return !IsLocked;
    }
}
