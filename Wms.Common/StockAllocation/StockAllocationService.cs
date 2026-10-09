using Wms.Outbound.Inventory;
using Wms.Outbound.Order;

namespace Wms.Outbound.StockAllocation;

public class StockAllocationService
{
    private readonly List<CustomerOrder> _orders;
    private readonly List<Sku> _skus;
    private readonly List<StockAllocation> _allocations = [];
    private readonly object _lockObject = new();

    public StockAllocationService(List<CustomerOrder> orders, List<Sku> skus)
    {
        _orders = orders;
        _skus = skus;
    }
    public bool AllocateReleaseOrders()
    {
        lock (_lockObject)
        {

            var availableBySku = _skus.ToDictionary(
                sku => sku.SkuId,
                sku => sku.ProductQuantity);

            // Iterate through the current allocations and subtract the allocated quantities from the available quantities
            foreach (var allocation in _allocations)
            {
                    availableBySku[allocation.SkuId] -= allocation.Quantity;
            }

            // Create a dictionary to track the allocated quantity for each order line
            var allocatedByLine = _allocations
                .GroupBy(allocation => (allocation.OrderId, allocation.LineId))
                .ToDictionary(group => group.Key, group => group.Sum(a => a.Quantity));

            // Prioritize orders based on their status, priority, and order date
            var queue = _orders
                .Where(order =>
                    order.Status == OrderStatus.Released)
                .OrderByDescending(order => order.Priority)
                .ThenBy(order => order.OrderDate)
                .ThenBy(order => order.OrderId);

            foreach (var order in queue)
            {
                // created a copy of the availableBySku and allocatedByLine dictionaries
                // if order cannot be fulfilled, original will not be modified
                var currentAvailability = new Dictionary<string, int>(availableBySku);
                var plan = new List<StockAllocation>();
                var canFulfillOrder = true;

                foreach (var line in order.Lines)
                {
                    var key = (order.OrderId, line.LineId);
                    var outstanding = line.Quantity -
                        allocatedByLine.GetValueOrDefault(key);

                    if (outstanding <= 0)
                        continue;

                    var matchingSkus = _skus
                        .Where(sku =>
                            !sku.Location.IsLocked &&
                            string.Equals(
                                sku.Product.ProductId,
                                line.ProductId,
                                StringComparison.Ordinal))
                        .OrderBy(sku => sku.Location.Id)
                        .ThenBy(sku => sku.SkuId);

                    foreach (var sku in matchingSkus)
                    {
                        if (outstanding == 0)
                            break;

                        var available = currentAvailability[sku.SkuId];
                        if (available <= 0)
                            continue;

                        var quantity = Math.Min(outstanding, available);
                        currentAvailability[sku.SkuId] -= quantity;
                        outstanding -= quantity;

                        plan.Add(new StockAllocation(
                            order.OrderId, line.LineId, sku.SkuId, quantity));
                    }

                    if (outstanding > 0)
                        canFulfillOrder = false;
                }

                if (order.CompleteDeliveryRequired && !canFulfillOrder)
                {
                    UpdateOrderStatus(order, allocatedByLine);
                    continue;
                }

                foreach (var allocation in plan)
                {
                    _allocations.Add(allocation);
                    availableBySku[allocation.SkuId] -= allocation.Quantity;

                    var key = (allocation.OrderId, allocation.LineId);
                    allocatedByLine[key] =
                        allocatedByLine.GetValueOrDefault(key) + allocation.Quantity;
                }

                UpdateOrderStatus(order, allocatedByLine);
            }

        }

        return true;
    }
    private static void UpdateOrderStatus(CustomerOrder order,Dictionary<(string OrderId, int OrderLineId), int> allocatedByLine)
    {
        var allLinesFulfilled = order.Lines.All(line =>
            allocatedByLine.GetValueOrDefault((order.OrderId, line.LineId)) >= line.Quantity);

        if (allLinesFulfilled)
        {
            order.Status = OrderStatus.ReadyForDelivery;
        }
        else if (order.Lines.Any(line =>
                     allocatedByLine.GetValueOrDefault((order.OrderId, line.LineId)) > 0))
        {
            order.Status = OrderStatus.PartiallyAllocated;
        }
        else
        {
            order.Status = OrderStatus.Released;
        }
    }
}
