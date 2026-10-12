using Wms.Outbound.Inventory.Domain;
using Wms.Outbound.Inventory.Interfaces;
using Wms.Outbound.Order.Domain;
using Wms.Outbound.Order.Interfaces;
using Wms.Outbound.StockAllocation.Domain;
using Wms.Outbound.StockAllocation.Interfaces;

namespace Wms.Outbound.StockAllocation.Services;

public class StockAllocationService
{
    private static readonly TimeSpan DefaultWaveWindow = TimeSpan.FromMilliseconds(500);

    private readonly IInventoryService _inventoryService;
    private readonly IOrderService _orderService;
    private readonly TimeSpan _waveWindow;
    private readonly SemaphoreSlim _allocationGate = new(1, 1);
    private readonly IOrderCache _orderCache;
    private readonly IAllocationLedger _allocationLedger;

    public StockAllocationService(IInventoryService inventoryService, IOrderService orderService, IOrderCache orderCache, IAllocationLedger allocationLedger, TimeSpan? waveWindow = null)
    {
        _inventoryService = inventoryService;
        _orderService = orderService;
        _orderCache = orderCache;
        _allocationLedger = allocationLedger;
        _waveWindow = waveWindow ?? DefaultWaveWindow;

        _ = ConsumeNewOrdersAsync();
    }

   
    private async Task ConsumeNewOrdersAsync()
    {
        var orderReader = _orderService.NewOrders;

        while (await orderReader.WaitToReadAsync())
        {
            await Task.Delay(_waveWindow);

            try
            {
                await RunAllocationAsync();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Stock allocation failed: {ex}");
            }
        }
    }

    public async Task<IReadOnlyList<StockAllocationResult>> RunAllocationAsync()
    {
        await _allocationGate.WaitAsync();
        try
        {
            var skus = (await _inventoryService.GetSkuListAsync()).ToList();
            var skuById = skus.ToDictionary(sku => sku.SkuId);
            var skusByProductId = BuildSkusByProductId(skus);

            var results = new List<StockAllocationResult>();
            var skusToUpdate = new Dictionary<string, Sku>();

            var availableQtyBySku = BuildAvailableQuantities(skus);

            var ordersToAllocate = GetOrdersToAllocate().ToList();

            foreach (var order in ordersToAllocate)
            {
                var plan = TryBuildAllocationPlan(order, skusByProductId, availableQtyBySku, _allocationLedger);

                if (order.CompleteDeliveryRequired && plan is null)
                {
                    ReleaseOrderAllocations(order.OrderId, skuById, availableQtyBySku, skusToUpdate, _allocationLedger);

                    order.RecalculateStatus(line => _allocationLedger.GetAllocatedQuantity(order.OrderId, line.LineId));
                    results.Add(new StockAllocationResult(order.OrderId, order.Status, 0));
                    continue;
                }

                var committedPlan = plan ?? [];
                CommitAllocationPlan(committedPlan, skuById, availableQtyBySku, skusToUpdate, _allocationLedger);

                order.RecalculateStatus(line => _allocationLedger.GetAllocatedQuantity(order.OrderId, line.LineId));
                results.Add(new StockAllocationResult(
                    order.OrderId,
                    order.Status,
                    committedPlan.Sum(allocation => allocation.Quantity)));
            }

            await Task.WhenAll(skusToUpdate.Values.Select(sku => _inventoryService.UpdateSkuAsync(sku)));

            return results;
        }
        finally
        {
            _allocationGate.Release();
        }
    }

    private IEnumerable<CustomerOrder> GetOrdersToAllocate() =>
        _orderCache.Orders.Values
            .Where(order =>
                (order.Status == OrderStatus.Released ||
                order.Status == OrderStatus.PartiallyAllocated) &&
                !order.IsCancled)
            .OrderByDescending(order => order.Priority)
            .ThenBy(order => order.OrderDate);


    private static Dictionary<string, int> BuildAvailableQuantities(IEnumerable<Sku> skus)
    {
        return skus.ToDictionary(sku => sku.SkuId, sku => sku.ProductQuantity);
    }

    private static Dictionary<string, List<Sku>> BuildSkusByProductId(IEnumerable<Sku> skus) =>
        skus
            .Where(sku => sku.Location.CanAllocateStock())
            .GroupBy(sku => sku.Product.ProductId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.Ordinal);

    private static List<OrderLineAllocation>? TryBuildAllocationPlan(
        CustomerOrder order,
        Dictionary<string, List<Sku>> skusByProductId,
        Dictionary<string, int> availableQtyBySku,
        IAllocationLedger allocationLedger)
    {
        var currentSkuAvailability = new Dictionary<string, int>(availableQtyBySku);
        var plan = new List<OrderLineAllocation>();
        var canFulfillOrder = true;

        foreach (var line in order.Lines.Where(line => !line.IsCancelled))
        {
            var outstanding = line.Quantity - allocationLedger.GetAllocatedQuantity(order.OrderId, line.LineId);

            if (outstanding <= 0)
                continue;

            var requiredSkus = skusByProductId.GetValueOrDefault(line.ProductId, []);

            foreach (var sku in requiredSkus)
            {
                if (outstanding == 0)
                    break;

                var available = currentSkuAvailability[sku.SkuId];
                if (available <= 0)
                    continue;

                var quantity = Math.Min(outstanding, available);
                currentSkuAvailability[sku.SkuId] -= quantity;
                outstanding -= quantity;

                plan.Add(new OrderLineAllocation(order.OrderId, line.LineId, sku.SkuId, quantity));
            }

            if (outstanding > 0)
                canFulfillOrder = false;
        }

        return order.CompleteDeliveryRequired && !canFulfillOrder ? null : plan;
    }

    private static void CommitAllocationPlan(
        IEnumerable<OrderLineAllocation> plan,
        Dictionary<string, Sku> skuById,
        Dictionary<string, int> availableQtyBySku,
        Dictionary<string, Sku> skusToUpdate,
        IAllocationLedger allocationLedger)
    {
        foreach (var allocation in plan)
        {
            availableQtyBySku[allocation.SkuId] -= allocation.Quantity;

            if (!skusToUpdate.TryGetValue(allocation.SkuId, out var sku))
            {
                sku = skuById[allocation.SkuId];
                skusToUpdate[allocation.SkuId] = sku;
            }

            sku.Reserve(allocation.Quantity);

            allocationLedger.Record(allocation);
        }
    }

    private static void ReleaseOrderAllocations(
        string orderId,
        Dictionary<string, Sku> skuById,
        Dictionary<string, int> availableQtyBySku,
        Dictionary<string, Sku> skusToUpdate,
        IAllocationLedger allocationLedger)
    {
        var releasedAllocations = allocationLedger.ReleaseOrder(orderId);

        foreach (var allocation in releasedAllocations)
        {
            availableQtyBySku[allocation.SkuId] = availableQtyBySku.GetValueOrDefault(allocation.SkuId) + allocation.Quantity;

            if (!skusToUpdate.TryGetValue(allocation.SkuId, out var sku))
            {
                sku = skuById[allocation.SkuId];
                skusToUpdate[allocation.SkuId] = sku;
            }

            sku.Release(allocation.Quantity);
        }
    }
}
