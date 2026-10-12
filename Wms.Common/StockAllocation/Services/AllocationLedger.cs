using System.Collections.Concurrent;
using Wms.Outbound.StockAllocation.Domain;
using Wms.Outbound.StockAllocation.Interfaces;

namespace Wms.Outbound.StockAllocation.Services
{
    public class AllocationLedger : IAllocationLedger
    {
        private readonly ConcurrentDictionary<string, ConcurrentDictionary<int, ConcurrentBag<OrderLineAllocation>>> _byOrder = new(StringComparer.Ordinal);

        public void Record(OrderLineAllocation allocation)
        {
            var lines = _byOrder.GetOrAdd(allocation.OrderId,  _ => new ConcurrentDictionary<int, ConcurrentBag<OrderLineAllocation>>());
            var bag = lines.GetOrAdd(allocation.LineId,  _ => []);
            bag.Add(allocation);
        }

        public IReadOnlyList<OrderLineAllocation> ReleaseOrder(string orderId)
        {
            if (!_byOrder.TryRemove(orderId, out var lines))
                return [];

            return [.. lines.Values.SelectMany(bag => bag)];
        }

        public IReadOnlyList<OrderLineAllocation> ReleaseLine(string orderId, int lineId)
        {
            if (!_byOrder.TryGetValue(orderId, out var lines) || !lines.TryRemove(lineId, out var bag))
                return [];

            return [.. bag];
        }

        public int GetAllocatedQuantity(string orderId, int lineId)
        {
            if (!_byOrder.TryGetValue(orderId, out var lines) || !lines.TryGetValue(lineId, out var bag))
                return 0;

            return bag.Sum(allocation => allocation.Quantity);
        }
    }
}
