using System.Threading.Channels;
using NSubstitute;
using Wms.Outbound.Inventory.Domain;
using Wms.Outbound.Inventory.Interfaces;
using Wms.Outbound.Order.Domain;
using Wms.Outbound.Order.Interfaces;
using Wms.Outbound.Order.Services;
using Wms.Outbound.StockAllocation.Domain;
using Wms.Outbound.StockAllocation.Services;

namespace Wms.Outbound.Tests.StockAllocation
{
    public class StockOperationsServiceTests
    {
        private static IOrderService CreateOrderService()
        {
            var orderService = Substitute.For<IOrderService>();
            orderService.NewOrders.Returns(Channel.CreateUnbounded<CustomerOrder>().Reader);
            orderService.Cancellations.Returns(Channel.CreateUnbounded<CustomerOrder>().Reader);
            return orderService;
        }

        private static StockAllocationService CreateStockAllocationService(IInventoryService inventoryService, IOrderService orderService, AllocationLedger allocationLedger, OrderCache orderCache)
        {
            return new StockAllocationService(inventoryService, orderService, orderCache, allocationLedger);
        }

        [Fact]
        public async Task DeallocateOperationAsync_WithWholeOrderCancelled_ReleasesAllLinesAndSetsCancelledStatus()
        {
            var inventoryService = Substitute.For<IInventoryService>();
            var orderService = CreateOrderService();
            var allocationLedger = new AllocationLedger();
            var orderCache = new OrderCache();

            var skuOne = TestDataFactory.CreateDefaultSku("SKU-001", 0);
            var skuTwo = TestDataFactory.CreateDefaultSku("SKU-002", 0);
            inventoryService.GetSkuByIdAsync("SKU-001").Returns(skuOne);
            inventoryService.GetSkuByIdAsync("SKU-002").Returns(skuTwo);
            inventoryService.GetSkuListAsync().Returns(new List<Sku>());

            var order = TestDataFactory.CreateDefaultOrder("ORD-001", 20);
            order.AddLine(TestDataFactory.CreateDefaultOrderLine(2, "1", 10));
            order.CancelWholeOrder();

            allocationLedger.Record(new OrderLineAllocation(order.OrderId, 1, "SKU-001", 20));
            allocationLedger.Record(new OrderLineAllocation(order.OrderId, 2, "SKU-002", 10));

            var stockAllocationService = CreateStockAllocationService(inventoryService, orderService, allocationLedger, orderCache);
            var stockOperationsService = new StockOperationsService(inventoryService, orderService, allocationLedger, stockAllocationService);

            await stockOperationsService.DeallocateOperationAsync(order);

            Assert.Equal(OrderStatus.Cancelled, order.Status);
            Assert.Equal(0, allocationLedger.GetAllocatedQuantity(order.OrderId, 1));
            Assert.Equal(0, allocationLedger.GetAllocatedQuantity(order.OrderId, 2));
            Assert.Equal(20, skuOne.ProductQuantity);
            Assert.Equal(10, skuTwo.ProductQuantity);
        }

        [Fact]
        public async Task DeallocateOperationAsync_WithOnlySomeLinesCancelled_ReleasesOnlyCancelledLines()
        {
            var inventoryService = Substitute.For<IInventoryService>();
            var orderService = CreateOrderService();
            var allocationLedger = new AllocationLedger();
            var orderCache = new OrderCache();

            var sku = TestDataFactory.CreateDefaultSku("SKU-001", 0);
            inventoryService.GetSkuByIdAsync("SKU-001").Returns(sku);
            inventoryService.GetSkuListAsync().Returns(new List<Sku>());

            var order = TestDataFactory.CreateDefaultOrder("ORD-001", 20);
            var cancelledLine = TestDataFactory.CreateDefaultOrderLine(2, "1", 10);
            cancelledLine.Cancel();
            order.AddLine(cancelledLine);

            allocationLedger.Record(new OrderLineAllocation(order.OrderId, 1, "SKU-001", 20));
            allocationLedger.Record(new OrderLineAllocation(order.OrderId, 2, "SKU-001", 10));

            var stockAllocationService = CreateStockAllocationService(inventoryService, orderService, allocationLedger, orderCache);
            var stockOperationsService = new StockOperationsService(inventoryService, orderService, allocationLedger, stockAllocationService);

            await stockOperationsService.DeallocateOperationAsync(order);

            Assert.Equal(20, allocationLedger.GetAllocatedQuantity(order.OrderId, 1));
            Assert.Equal(0, allocationLedger.GetAllocatedQuantity(order.OrderId, 2));
            Assert.Equal(10, sku.ProductQuantity);
        }

        [Fact]
        public async Task DeallocateOperationAsync_WithNoAllocatedStockToRelease_DoesNotCallInventory()
        {
            var inventoryService = Substitute.For<IInventoryService>();
            var orderService = CreateOrderService();
            var allocationLedger = new AllocationLedger();
            var orderCache = new OrderCache();
            inventoryService.GetSkuListAsync().Returns(new List<Sku>());

            var order = TestDataFactory.CreateDefaultOrder("ORD-001", 20);
            order.CancelWholeOrder();

            var stockAllocationService = CreateStockAllocationService(inventoryService, orderService, allocationLedger, orderCache);
            var stockOperationsService = new StockOperationsService(inventoryService, orderService, allocationLedger, stockAllocationService);

            await stockOperationsService.DeallocateOperationAsync(order);

            Assert.Equal(OrderStatus.Cancelled, order.Status);
            await inventoryService.DidNotReceive().GetSkuByIdAsync(Arg.Any<string>());
            await inventoryService.DidNotReceive().UpdateSkuAsync(Arg.Any<Sku>());
        }

        [Fact]
        public async Task DeallocateOperationAsync_WithTwoAllocationsOnSameSku_UpdatesInventoryOnceWithCombinedQuantity()
        {
            var inventoryService = Substitute.For<IInventoryService>();
            var orderService = CreateOrderService();
            var allocationLedger = new AllocationLedger();
            var orderCache = new OrderCache();

            var sku = TestDataFactory.CreateDefaultSku("SKU-001", 0);
            inventoryService.GetSkuByIdAsync("SKU-001").Returns(sku);
            inventoryService.GetSkuListAsync().Returns(new List<Sku>());

            var order = TestDataFactory.CreateDefaultOrder("ORD-001", 30);
            order.CancelWholeOrder();

            allocationLedger.Record(new OrderLineAllocation(order.OrderId, 1, "SKU-001", 20));
            allocationLedger.Record(new OrderLineAllocation(order.OrderId, 1, "SKU-001", 10));

            var stockAllocationService = CreateStockAllocationService(inventoryService, orderService, allocationLedger, orderCache);
            var stockOperationsService = new StockOperationsService(inventoryService, orderService, allocationLedger, stockAllocationService);

            await stockOperationsService.DeallocateOperationAsync(order);

            Assert.Equal(30, sku.ProductQuantity);
            await inventoryService.Received(1).GetSkuByIdAsync("SKU-001");
            await inventoryService.Received(1).UpdateSkuAsync(sku);
        }

        [Fact]
        public async Task DeallocateOperationAsync_AfterReleasingStock_FreedStockAllocatesWaitingOrder()
        {
            var inventoryService = Substitute.For<IInventoryService>();
            var orderService = CreateOrderService();
            var allocationLedger = new AllocationLedger();
            var orderCache = new OrderCache();

            var sku = TestDataFactory.CreateDefaultSku("SKU-001", 0);
            inventoryService.GetSkuByIdAsync("SKU-001").Returns(sku);
            inventoryService.GetSkuListAsync().Returns(new List<Sku> { sku });
            inventoryService.UpdateSkuAsync(Arg.Any<Sku>()).Returns(true);

            var cancelledOrder = TestDataFactory.CreateDefaultOrder("ORD-001", 20);
            cancelledOrder.CancelWholeOrder();
            allocationLedger.Record(new OrderLineAllocation(cancelledOrder.OrderId, 1, "SKU-001", 20));

            var waitingOrder = TestDataFactory.CreateDefaultOrder("ORD-002", 20);
            orderCache.AddOrUpdate(waitingOrder);

            var stockAllocationService = CreateStockAllocationService(inventoryService, orderService, allocationLedger, orderCache);
            var stockOperationsService = new StockOperationsService(inventoryService, orderService, allocationLedger, stockAllocationService);

            await stockOperationsService.DeallocateOperationAsync(cancelledOrder);

            Assert.Equal(OrderStatus.ReadyForDelivery, waitingOrder.Status);
            Assert.Equal(0, sku.ProductQuantity);
        }
    }
}
