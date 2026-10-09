namespace Wms.Outbound.Inventory;

public class WarehouseLocation
{
    public string Id { get; set; } = default!;
    public bool IsLocked { get; private set; }

    public bool CanAllocateStock()
    {
        return !IsLocked;
    }
}
