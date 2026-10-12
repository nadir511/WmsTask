using Wms.Outbound.StockAllocation.Domain;

namespace Wms.Outbound.StockAllocation.Interfaces
{
    /// <summary>
    /// Tracks which SKUs (and quantities) back each order lines committed
    /// allocation, so that a cancellation can release precisely those quantities
    /// back to inventory.
    /// </summary>
    public interface IAllocationLedger
    {
        void Record(OrderLineAllocation allocation);

        IReadOnlyList<OrderLineAllocation> ReleaseOrder(string orderId);

        IReadOnlyList<OrderLineAllocation> ReleaseLine(string orderId, int lineId);

        int GetAllocatedQuantity(string orderId, int lineId);
    }
}
