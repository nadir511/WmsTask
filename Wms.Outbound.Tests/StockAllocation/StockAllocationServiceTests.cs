using System.Threading.Channels;
using NSubstitute;
using Wms.Outbound.Inventory.Domain;
using Wms.Outbound.Inventory.Interfaces;
using Wms.Outbound.Inventory.Services;
using Wms.Outbound.Order.Domain;
using Wms.Outbound.Order.Interfaces;
using Wms.Outbound.Order.Services;
using Wms.Outbound.StockAllocation.Domain;
using Wms.Outbound.StockAllocation.Services;

namespace Wms.Outbound.Tests.StockAllocation
{
    public class StockAllocationServiceTests
    {
        [Fact]
        public async Task AllocateReleaseOrders_OneOrderWriteToChannel_ReturnsReadyForDelivery()
        {
            // Arrange
            var inventoryService = Substitute.For<IInventoryService>();
            var orderService = Substitute.For<IOrderService>();

            var location = TestDataFactory.CreateDefaultLocation();
            var product = TestDataFactory.CreateDefaultProduct();

            var sku = TestDataFactory.CreateDefaultSku();

            inventoryService.GetSkuListAsync().Returns(new List<Sku> { sku });
            inventoryService.UpdateSkuAsync(Arg.Any<Sku>()).Returns(true);


            var order = TestDataFactory.CreateDefaultOrder();

            var newOrders = Channel.CreateUnbounded<CustomerOrder>();
            orderService.NewOrders.Returns(newOrders.Reader);

            var orderCache = new OrderCache();
            orderCache.AddOrUpdate(order);

            var allocationLedger = new AllocationLedger();

            // Act
            var allocationService = new StockAllocationService(inventoryService, orderService, orderCache, allocationLedger);
            await newOrders.Writer.WriteAsync(order);

            var deadline = DateTime.UtcNow.AddSeconds(2);
            while (order.Status == OrderStatus.Released && DateTime.UtcNow < deadline)
            {
                await Task.Delay(10);
            }

            // Assert
            Assert.Equal(OrderStatus.ReadyForDelivery, order.Status);
            await inventoryService.Received(1).UpdateSkuAsync(Arg.Is<Sku>(s => s.SkuId == "SKU-001" && s.ProductQuantity == 70));
        }
        [Fact]
        public async Task RunAllocationAsync_WithTwoOrdersOneReadyAndOtherPartiallyAllocated_ReturnsReadyForDeliveryAndPartiallyAllocated()
        {
            var inventoryService = Substitute.For<IInventoryService>();
            var orderService = Substitute.For<IOrderService>();

            var sku = TestDataFactory.CreateDefaultSku("SKU-001", 50);
            inventoryService.GetSkuListAsync().Returns(new List<Sku> { sku });
            inventoryService.UpdateSkuAsync(Arg.Any<Sku>()).Returns(true);

            var order1 = TestDataFactory.CreateDefaultOrder("ORD-001", 30);
            var order2 = TestDataFactory.CreateDefaultOrder("ORD-002", 80);

            var orderCache = new OrderCache();
            orderCache.AddOrUpdate(order1);
            orderCache.AddOrUpdate(order2);

            var allocationService = new StockAllocationService(inventoryService, orderService, orderCache, new AllocationLedger());

            await allocationService.RunAllocationAsync();

            Assert.Equal(OrderStatus.ReadyForDelivery, order1.Status);
            Assert.Equal(OrderStatus.PartiallyAllocated, order2.Status);
        }

        [Fact]
        public async Task RunAllocationAsync_WithInsufficientStockAndCompleteDeliveryRequired_OrderStaysReleasedWithNoAllocation()
        {
            var inventoryService = Substitute.For<IInventoryService>();
            var orderService = Substitute.For<IOrderService>();

            var sku = TestDataFactory.CreateDefaultSku("SKU-001", 20);
            inventoryService.GetSkuListAsync().Returns(new List<Sku> { sku });
            inventoryService.UpdateSkuAsync(Arg.Any<Sku>()).Returns(true);

            var order = TestDataFactory.CreateDefaultOrder("ORD-001", 50, completeDeliveryRequired: true);

            var orderCache = new OrderCache();
            orderCache.AddOrUpdate(order);

            var allocationService = new StockAllocationService(inventoryService, orderService, orderCache, new AllocationLedger());

            var results = await allocationService.RunAllocationAsync();

            Assert.Equal(OrderStatus.Released, order.Status);
            Assert.Equal(0, results.Single().NewlyAllocatedQuantity);
            await inventoryService.DidNotReceive().UpdateSkuAsync(Arg.Any<Sku>());
        }

        [Fact]
        public async Task RunAllocationAsync_WithCompleteDeliveryRequiredOrderHoldingStaleAllocation_ReleasesStockBackToInventory()
        {
            var inventoryService = Substitute.For<IInventoryService>();
            var orderService = Substitute.For<IOrderService>();

            var sku = TestDataFactory.CreateDefaultSku("SKU-001", 20);
            inventoryService.GetSkuListAsync().Returns(new List<Sku> { sku });
            inventoryService.UpdateSkuAsync(Arg.Any<Sku>()).Returns(true);

            var order = TestDataFactory.CreateDefaultOrder("ORD-001", 50, completeDeliveryRequired: true);

            var orderCache = new OrderCache();
            orderCache.AddOrUpdate(order);

            var allocationLedger = new AllocationLedger();
            allocationLedger.Record(new OrderLineAllocation(order.OrderId, order.Lines[0].LineId, sku.SkuId, 20));

            var allocationService = new StockAllocationService(inventoryService, orderService, orderCache, allocationLedger);

            var results = await allocationService.RunAllocationAsync();

            Assert.Equal(OrderStatus.Released, order.Status);
            Assert.Equal(0, results.Single().NewlyAllocatedQuantity);
            Assert.Equal(40, sku.ProductQuantity);
            Assert.Equal(0, allocationLedger.GetAllocatedQuantity(order.OrderId, order.Lines[0].LineId));
            await inventoryService.Received(1).UpdateSkuAsync(Arg.Is<Sku>(s => s.SkuId == sku.SkuId));
        }

        [Fact]
        public async Task RunAllocationAsync_WithOneCancelledLine_SkipsCancelledLineAndAllocatesOtherLine()
        {
            var inventoryService = Substitute.For<IInventoryService>();
            var orderService = Substitute.For<IOrderService>();

            var skuOne = TestDataFactory.CreateDefaultSku("SKU-001", 100, TestDataFactory.CreateDefaultProduct("1"));
            var skuTwo = TestDataFactory.CreateDefaultSku("SKU-002", 20, TestDataFactory.CreateDefaultProduct("2"));
            inventoryService.GetSkuListAsync().Returns(new List<Sku> { skuOne, skuTwo });
            inventoryService.UpdateSkuAsync(Arg.Any<Sku>()).Returns(true);

            var cancelledLine = TestDataFactory.CreateDefaultOrderLine(1, "1", 30);
            cancelledLine.Cancel();
            var activeLine = TestDataFactory.CreateDefaultOrderLine(2, "2", 20);
            var order = TestDataFactory.CreateDefaultOrder("ORD-001", orderLine: cancelledLine);
            order.AddLine(activeLine);

            var orderCache = new OrderCache();
            orderCache.AddOrUpdate(order);

            var allocationService = new StockAllocationService(inventoryService, orderService, orderCache, new AllocationLedger());

            await allocationService.RunAllocationAsync();

            Assert.Equal(OrderStatus.ReadyForDelivery, order.Status);
            await inventoryService.DidNotReceive().UpdateSkuAsync(Arg.Is<Sku>(s => s.SkuId == "SKU-001"));
            await inventoryService.Received(1).UpdateSkuAsync(Arg.Is<Sku>(s => s.SkuId == "SKU-002" && s.ProductQuantity == 0));
        }

        [Fact]
        public async Task RunAllocationAsync_WithInsufficientStockForBothOrders_AllocatesHigherPriorityOrderFirst()
        {
            var inventoryService = Substitute.For<IInventoryService>();
            var orderService = Substitute.For<IOrderService>();

            var sku = TestDataFactory.CreateDefaultSku("SKU-001", 40);
            inventoryService.GetSkuListAsync().Returns(new List<Sku> { sku });
            inventoryService.UpdateSkuAsync(Arg.Any<Sku>()).Returns(true);

            var lowPriorityOrder = TestDataFactory.CreateDefaultOrder("ORD-001", 30, orderPriority: OrderPriority.Low);
            var highPriorityOrder = TestDataFactory.CreateDefaultOrder("ORD-002", 30, orderPriority: OrderPriority.High);

            var orderCache = new OrderCache();
            orderCache.AddOrUpdate(lowPriorityOrder);
            orderCache.AddOrUpdate(highPriorityOrder);

            var allocationService = new StockAllocationService(inventoryService, orderService, orderCache, new AllocationLedger());
            await allocationService.RunAllocationAsync();

            Assert.Equal(OrderStatus.ReadyForDelivery, highPriorityOrder.Status);
            Assert.Equal(OrderStatus.PartiallyAllocated, lowPriorityOrder.Status);
        }

        [Fact]
        public async Task RunAllocationAsync_CalledAgainAfterRestock_CompletesOrderWithoutDoubleAllocating()
        {
            var inventoryService = Substitute.For<IInventoryService>();
            var orderService = Substitute.For<IOrderService>();

            var sku = TestDataFactory.CreateDefaultSku("SKU-001", 20);
            inventoryService.GetSkuListAsync().Returns(new List<Sku> { sku });
            inventoryService.UpdateSkuAsync(Arg.Any<Sku>()).Returns(true);

            var order = TestDataFactory.CreateDefaultOrder("ORD-001", 50);

            IOrderCache orderCache = new OrderCache();
            orderCache.AddOrUpdate(order);

            var allocationService = new StockAllocationService(inventoryService, orderService, orderCache, new AllocationLedger());

            await allocationService.RunAllocationAsync();
            Assert.Equal(OrderStatus.PartiallyAllocated, order.Status);

            sku.Release(30);
            await allocationService.RunAllocationAsync();

            Assert.Equal(OrderStatus.ReadyForDelivery, order.Status);
            Assert.Equal(0, sku.ProductQuantity);
        }

        [Fact]
        public async Task RunAllocationAsync_WithOrderSplitAcrossTwoSkusOfSameProduct_AllocatesFromBothSkus()
        {
            var inventoryService = Substitute.For<IInventoryService>();
            var orderService = Substitute.For<IOrderService>();

            var product = TestDataFactory.CreateDefaultProduct("1");
            var skuOne = TestDataFactory.CreateDefaultSku("SKU-001", 10, product);
            var skuTwo = TestDataFactory.CreateDefaultSku("SKU-002", 50, product);
            inventoryService.GetSkuListAsync().Returns(new List<Sku> { skuOne, skuTwo });
            inventoryService.UpdateSkuAsync(Arg.Any<Sku>()).Returns(true);

            var order = TestDataFactory.CreateDefaultOrder("ORD-001", 30);

            var orderCache = new OrderCache();
            orderCache.AddOrUpdate(order);

            var allocationService = new StockAllocationService(inventoryService, orderService, orderCache, new AllocationLedger());

            await allocationService.RunAllocationAsync();

            Assert.Equal(OrderStatus.ReadyForDelivery, order.Status);
            Assert.Equal(0, skuOne.ProductQuantity);
            Assert.Equal(30, skuTwo.ProductQuantity);
        }

        [Fact]
        public async Task RunAllocationAsync_WithOnlyLockedLocationSkuAvailable_OrderStaysReleasedWithNoAllocation()
        {
            var inventoryService = Substitute.For<IInventoryService>();
            var orderService = Substitute.For<IOrderService>();

            var lockedLocation = TestDataFactory.CreateDefaultLocation("LOC-001", isLocked: true);
            var sku = TestDataFactory.CreateDefaultSku("SKU-001", 100, location: lockedLocation);
            inventoryService.GetSkuListAsync().Returns(new List<Sku> { sku });
            inventoryService.UpdateSkuAsync(Arg.Any<Sku>()).Returns(true);

            var order = TestDataFactory.CreateDefaultOrder("ORD-001", 30);

            var orderCache = new OrderCache();
            orderCache.AddOrUpdate(order);

            var allocationService = new StockAllocationService(inventoryService, orderService, orderCache, new AllocationLedger());

            await allocationService.RunAllocationAsync();

            Assert.Equal(OrderStatus.Released, order.Status);
            await inventoryService.DidNotReceive().UpdateSkuAsync(Arg.Any<Sku>());
        }

        [Fact]
        public async Task RunAllocationAsync_WithNoMatchingSkuForProduct_OrderStaysReleasedWithNoAllocation()
        {
            var inventoryService = Substitute.For<IInventoryService>();
            var orderService = Substitute.For<IOrderService>();

            var sku = TestDataFactory.CreateDefaultSku("SKU-002", 100, TestDataFactory.CreateDefaultProduct("2"));
            inventoryService.GetSkuListAsync().Returns(new List<Sku> { sku });
            inventoryService.UpdateSkuAsync(Arg.Any<Sku>()).Returns(true);

            var order = TestDataFactory.CreateDefaultOrder("ORD-001", 30);

            var orderCache = new OrderCache();
            orderCache.AddOrUpdate(order);

            var allocationService = new StockAllocationService(inventoryService, orderService, orderCache, new AllocationLedger());

            var results = await allocationService.RunAllocationAsync();

            Assert.Equal(OrderStatus.Released, order.Status);
            Assert.Equal(0, results.Single().NewlyAllocatedQuantity);
            await inventoryService.DidNotReceive().UpdateSkuAsync(Arg.Any<Sku>());
        }

        [Fact]
        public async Task RunAllocationAsync_WithCancelledOrder_IsNotProcessed()
        {
            var inventoryService = Substitute.For<IInventoryService>();
            var orderService = Substitute.For<IOrderService>();

            var sku = TestDataFactory.CreateDefaultSku("SKU-001", 100);
            inventoryService.GetSkuListAsync().Returns(new List<Sku> { sku });
            inventoryService.UpdateSkuAsync(Arg.Any<Sku>()).Returns(true);

            var order = TestDataFactory.CreateDefaultOrder("ORD-001", 30);
            order.CancelWholeOrder();

            var orderCache = new OrderCache();
            orderCache.AddOrUpdate(order);

            var allocationService = new StockAllocationService(inventoryService, orderService, orderCache, new AllocationLedger());

            var results = await allocationService.RunAllocationAsync();

            Assert.Empty(results);
            Assert.Equal(OrderStatus.Released, order.Status);
            await inventoryService.DidNotReceive().UpdateSkuAsync(Arg.Any<Sku>());
        }

        [Fact]
        public async Task RunAllocationAsync_WithOrderAlreadyReadyForDelivery_IsNotReprocessed()
        {
            var inventoryService = Substitute.For<IInventoryService>();
            var orderService = Substitute.For<IOrderService>();

            var sku = TestDataFactory.CreateDefaultSku("SKU-001", 100);
            inventoryService.GetSkuListAsync().Returns(new List<Sku> { sku });
            inventoryService.UpdateSkuAsync(Arg.Any<Sku>()).Returns(true);

            var order = TestDataFactory.CreateDefaultOrder("ORD-001", 30);
            order.RecalculateStatus(_ => int.MaxValue);

            var orderCache = new OrderCache();
            orderCache.AddOrUpdate(order);

            var allocationService = new StockAllocationService(inventoryService, orderService, orderCache, new AllocationLedger());

            var results = await allocationService.RunAllocationAsync();

            Assert.Empty(results);
            await inventoryService.DidNotReceive().UpdateSkuAsync(Arg.Any<Sku>());
        }

        [Fact]
        public async Task RunAllocationAsync_WithNoOrdersInCache_ReturnsEmptyResultsAndNoInventoryUpdates()
        {
            var inventoryService = Substitute.For<IInventoryService>();
            var orderService = Substitute.For<IOrderService>();

            var sku = TestDataFactory.CreateDefaultSku("SKU-001", 100);
            inventoryService.GetSkuListAsync().Returns(new List<Sku> { sku });

            var orderCache = new OrderCache();

            var allocationService = new StockAllocationService(inventoryService, orderService, orderCache, new AllocationLedger());

            var results = await allocationService.RunAllocationAsync();

            Assert.Empty(results);
            await inventoryService.DidNotReceive().UpdateSkuAsync(Arg.Any<Sku>());
        }

        [Fact]
        public async Task RunAllocationAsync_CalledConcurrently_DoesNotOverAllocateStock()
        {
            var inventoryService = Substitute.For<IInventoryService>();
            var orderService = Substitute.For<IOrderService>();

            var sku = TestDataFactory.CreateDefaultSku("SKU-001", 30);
            inventoryService.GetSkuListAsync().Returns(new List<Sku> { sku });
            inventoryService.UpdateSkuAsync(Arg.Any<Sku>()).Returns(true);

            var order = TestDataFactory.CreateDefaultOrder("ORD-001", 30);

            var orderCache = new OrderCache();
            orderCache.AddOrUpdate(order);

            var allocationService = new StockAllocationService(inventoryService, orderService, orderCache, new AllocationLedger());

            await Task.WhenAll(
                allocationService.RunAllocationAsync(),
                allocationService.RunAllocationAsync());

            Assert.Equal(OrderStatus.ReadyForDelivery, order.Status);
            Assert.Equal(0, sku.ProductQuantity);
            await inventoryService.Received(1).UpdateSkuAsync(Arg.Any<Sku>());
        }
    }
}
