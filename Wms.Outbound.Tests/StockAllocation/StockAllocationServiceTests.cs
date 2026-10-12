using System.Threading.Channels;
using NSubstitute;
using Wms.Outbound.Inventory.Domain;
using Wms.Outbound.Inventory.Interfaces;
using Wms.Outbound.Inventory.Services;
using Wms.Outbound.Order.Domain;
using Wms.Outbound.Order.Interfaces;
using Wms.Outbound.Order.Services;
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
            // Arrange
            var inventoryService = Substitute.For<IInventoryService>();
            var orderService = Substitute.For<IOrderService>();

            var location = TestDataFactory.CreateDefaultLocation();
            var product = TestDataFactory.CreateDefaultProduct();
            var sku = TestDataFactory.CreateDefaultSku();

            inventoryService.GetSkuListAsync().Returns(new List<Sku> { sku });
            inventoryService.UpdateSkuAsync(Arg.Any<Sku>()).Returns(true);
            var order1 = TestDataFactory.CreateDefaultOrder("ORD-001", 30);
            var order2 = TestDataFactory.CreateDefaultOrder("ORD-002", 80);
           

            var orderCache = new OrderCache();
            orderCache.AddOrUpdate(order1);
            orderCache.AddOrUpdate(order2);
            var allocationLedger = new AllocationLedger();
            // Act
            var allocationService = new StockAllocationService(inventoryService, orderService, orderCache, allocationLedger);

            var result = await allocationService.RunAllocationAsync();
            // Assert
            Assert.Equal(OrderStatus.ReadyForDelivery, order1.Status);
            Assert.Equal(OrderStatus.PartiallyAllocated, order2.Status);
        }
    }
}
