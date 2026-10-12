using Wms.Outbound.Inventory.Interfaces;
using Wms.Outbound.Order.Domain;
using Wms.Outbound.Order.Interfaces;
using Wms.Outbound.StockAllocation.Domain;
using Wms.Outbound.StockAllocation.Interfaces;

namespace Wms.Outbound.StockAllocation.Services
{
    public class StockOperationsService : IStockOperationsService
    {
        private readonly IInventoryService _inventoryService;
        private readonly IOrderService _orderService;
        private readonly IAllocationLedger _allocationLedger;
        private readonly StockAllocationService _stockAllocationService;

        public StockOperationsService(
            IInventoryService inventoryService,
            IOrderService orderService,
            IAllocationLedger allocationLedger,
            StockAllocationService stockAllocationService)
        {
            _inventoryService = inventoryService;
            _orderService = orderService;
            _allocationLedger = allocationLedger;
            _stockAllocationService = stockAllocationService;

            _ = ConsumeCancellationsAsync();
        }

        public async Task DeallocateOperationAsync(CustomerOrder order)
        {
            List<OrderLineAllocation> released;

            if (order.IsCancled)
            {
                released = [.. _allocationLedger.ReleaseOrder(order.OrderId)];
                order.MarkCancelled();
            }
            else
            {
                released = [];
                foreach (var line in order.Lines.Where(l => l.IsCancelled))
                {
                    released.AddRange(_allocationLedger.ReleaseLine(order.OrderId, line.LineId));
                }
            }

            if (released.Count == 0)
                return;

            await ReleaseSkuQuantitiesAsync(released);

            await _stockAllocationService.RunAllocationAsync();
        }

        private async Task ConsumeCancellationsAsync()
        {
            await foreach (var order in _orderService.Cancellations.ReadAllAsync())
            {
                try
                {
                    await DeallocateOperationAsync(order);
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Stock deallocation failed for order {order.OrderId}: {ex}");
                }
            }
        }

        private async Task ReleaseSkuQuantitiesAsync(IReadOnlyList<OrderLineAllocation> released)
        {
            var releasedBySku = released
                .GroupBy(allocation => allocation.SkuId, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.Sum(allocation => allocation.Quantity), StringComparer.Ordinal);

            foreach (var (skuId, quantity) in releasedBySku)
            {
                var sku = await _inventoryService.GetSkuByIdAsync(skuId);
                sku.Release(quantity);
                await _inventoryService.UpdateSkuAsync(sku);
            }
        }
    }
}
