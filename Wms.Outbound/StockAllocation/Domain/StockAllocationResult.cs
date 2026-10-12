using Wms.Outbound.Order.Domain;

namespace Wms.Outbound.StockAllocation.Domain
{
    public sealed record StockAllocationResult(string OrderId, OrderStatus Status, int NewlyAllocatedQuantity);
}
