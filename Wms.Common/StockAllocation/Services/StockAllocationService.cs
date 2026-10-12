using Wms.Outbound.Inventory.Domain;
using Wms.Outbound.Inventory.Interfaces;
using Wms.Outbound.Order.Domain;
using Wms.Outbound.Order.Interfaces;
using Wms.Outbound.StockAllocation.Domain;

namespace Wms.Outbound.StockAllocation.Services;

public class StockAllocationService
{
    private static readonly TimeSpan DefaultWaveWindow = TimeSpan.FromMilliseconds(500);

    private readonly IInventoryService _inventoryService;
    private readonly IOrderService _orderService;
    private readonly TimeSpan _waveWindow;
    private readonly Dictionary<(string OrderId, int LineId), int> _allocatedQtyByLine = [];
    private readonly SemaphoreSlim _allocationGate = new(1, 1);
    private int _rerunRequested;
    private int _waveScheduled;

    public StockAllocationService(IInventoryService inventoryService, IOrderService orderService, TimeSpan? waveWindow = null)
    {
        _inventoryService = inventoryService;
        _orderService = orderService;
        _waveWindow = waveWindow ?? DefaultWaveWindow;
        orderService.NewOrderReceived += OnNewOrderReceived;
    }

    public async Task<IReadOnlyList<StockAllocationResult>> AllocateReleaseOrdersAsync()
    {
        await _allocationGate.WaitAsync();
        try
        {
            return await RunUntilStableAsync();
        }
        finally
        {
            _allocationGate.Release();
        }
    }

    private async Task<IReadOnlyList<StockAllocationResult>> RunUntilStableAsync()
    {
        IReadOnlyList<StockAllocationResult> results;
        do
        {
            Volatile.Write(ref _rerunRequested, 0);
            results = await RunAllocationAsync();
        } while (Volatile.Read(ref _rerunRequested) == 1);

        return results;
    }

    private async Task<IReadOnlyList<StockAllocationResult>> RunAllocationAsync()
    {
        var orders = await _orderService.GetCachedOrderListAsync();
        var skus = (await _inventoryService.GetSkuListAsync()).ToList();
        var skuById = skus.ToDictionary(sku => sku.SkuId);

        var results = new List<StockAllocationResult>();
        var skusToUpdate = new Dictionary<string, Sku>();

        var availableQtyBySku = BuildAvailableQuantities(skus);

        foreach (var order in GetOrdersToAllocate(orders))
        {
            var plan = TryBuildAllocationPlan(order, skus, availableQtyBySku, _allocatedQtyByLine);

            if (order.CompleteDeliveryRequired && plan is null)
            {
                UpdateOrderStatus(order, _allocatedQtyByLine);
                results.Add(new StockAllocationResult(order.OrderId, order.Status, 0));
                continue;
            }

            var committedPlan = plan ?? [];
            CommitAllocationPlan(committedPlan, skuById, availableQtyBySku, _allocatedQtyByLine, skusToUpdate);

            UpdateOrderStatus(order, _allocatedQtyByLine);
            results.Add(new StockAllocationResult(
                order.OrderId,
                order.Status,
                committedPlan.Sum(allocation => allocation.Quantity)));
        }

        await Task.WhenAll(skusToUpdate.Values.Select(sku => _inventoryService.UpdateSkuAsync(sku)));

        return results;
    }

    private static IEnumerable<CustomerOrder> GetOrdersToAllocate(IEnumerable<CustomerOrder> orders) =>
        orders
            .Where(order =>
                order.Status == OrderStatus.Released ||
                order.Status == OrderStatus.PartiallyAllocated)
            .OrderByDescending(order => order.Priority)
            .ThenBy(order => order.OrderDate);


    private static Dictionary<string, int> BuildAvailableQuantities(IEnumerable<Sku> skus)
    {
        return skus.ToDictionary(sku => sku.SkuId, sku => sku.ProductQuantity);
    }

    private static List<OrderLineAllocation>? TryBuildAllocationPlan(
        CustomerOrder order,
        IEnumerable<Sku> skus,
        Dictionary<string, int> availableQtyBySku,
        Dictionary<(string OrderId, int LineId), int> allocatedByLine)
    {
        var currentSkuAvailability = new Dictionary<string, int>(availableQtyBySku);
        var plan = new List<OrderLineAllocation>();
        var canFulfillOrder = true;

        foreach (var line in order.Lines)
        {
            var allocationKey = (order.OrderId, line.LineId);
            var outstanding = line.Quantity - allocatedByLine.GetValueOrDefault(allocationKey);

            //If the outstanding quantity is less than or equal to zero,
            //it means that the line has already been fully allocated, so we can skip to the next line.
            if (outstanding <= 0)
                continue;

            // Find all SKUs which are required to fulfill the order line.
            var requiredSkus = skus
                .Where(sku =>
                    !sku.Location.IsLocked &&
                    string.Equals(sku.Product.ProductId, line.ProductId, StringComparison.Ordinal));

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
        Dictionary<(string OrderId, int LineId), int> allocatedByLine,
        Dictionary<string, Sku> skusToUpdate)
    {
        foreach (var allocation in plan)
        {
            availableQtyBySku[allocation.SkuId] -= allocation.Quantity;

            var key = (allocation.OrderId, allocation.LineId);
            allocatedByLine[key] = allocatedByLine.GetValueOrDefault(key) + allocation.Quantity;

            if (!skusToUpdate.TryGetValue(allocation.SkuId, out var sku))
            {
                sku = skuById[allocation.SkuId];
                skusToUpdate[allocation.SkuId] = sku;
            }

            sku.ProductQuantity -= allocation.Quantity;
        }
    }

    private static void UpdateOrderStatus(CustomerOrder order, Dictionary<(string OrderId, int LineId), int> allocatedByLine)
    {
        var allLinesFulfilled = order.Lines.All(line =>
            allocatedByLine.GetValueOrDefault((order.OrderId, line.LineId)) >= line.Quantity);

        if (allLinesFulfilled)
        {
            order.Status = OrderStatus.ReadyForDelivery;
        }
        else if (order.Lines.Any(line => allocatedByLine.GetValueOrDefault((order.OrderId, line.LineId)) > 0))
        {
            order.Status = OrderStatus.PartiallyAllocated;
        }
        else
        {
            order.Status = OrderStatus.Released;
        }
    }

    private void OnNewOrderReceived(CustomerOrder order)
    {
        Volatile.Write(ref _rerunRequested, 1);

        if (Interlocked.CompareExchange(ref _waveScheduled, 1, 0) == 0)
        {
            _ = ReleaseWaveAsync();
        }
    }

    private async Task ReleaseWaveAsync()
    {
        try
        {
            await Task.Delay(_waveWindow);
            await TriggerAllocationAsync();
        }
        finally
        {
            Volatile.Write(ref _waveScheduled, 0);
        }
    }

    private async Task TriggerAllocationAsync()
    {
        if (!await _allocationGate.WaitAsync(0))
        {
            return;
        }

        try
        {
            await RunUntilStableAsync();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Stock allocation failed: {ex}");
        }
        finally
        {
            _allocationGate.Release();
        }
    }
}
