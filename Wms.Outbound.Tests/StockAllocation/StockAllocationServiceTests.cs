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
        public async Task AllocateReleaseOrders_ShouldAllocateOrderSuccessfullyWhenSufficientStockAvailable()
        {
            // Arrange
            var inventoryService = Substitute.For<IInventoryService>();
            var orderService = Substitute.For<IOrderService>();

            var location = new WarehouseLocation { Id = "LOC-001", IsLocked = false };
            var product = new Product { ProductId = "1", ProductName = "Orange Juice" };

            var sku = new Sku
            {
                SkuId = "SKU-001",
                ProductQuantity = 100,
                Product = product,
                Location = location
            };

            inventoryService.GetSkuListAsync().Returns(new List<Sku> { sku });
            inventoryService.UpdateSkuAsync(Arg.Any<Sku>()).Returns(true);

            var orderLine = new OrderLine(1, "1", 30);


            var OrdersLines = new List<OrderLine> { orderLine };

            var order = new CustomerOrder
            {
                OrderId = "ORD-001",
                CustomerId = "CUST-001",
                Lines = new List<OrderLine> { orderLine },
                Status = OrderStatus.Released,
                Priority = OrderPriority.High,
                OrderDate = DateTime.Now,
                CompleteDeliveryRequired = false
            };

            orderService.GetCachedOrderListAsync().Returns(new List<CustomerOrder> { order });

            var service = new StockAllocationService(inventoryService, orderService);

            // Act
            var results = await service.AllocateReleaseOrdersAsync();

            // Assert
            Assert.NotNull(results);
            Assert.Single(results);
            Assert.Equal("ORD-001", results[0].OrderId);
            Assert.Equal(OrderStatus.ReadyForDelivery, results[0].Status);
            Assert.Equal(30, results[0].NewlyAllocatedQuantity);

            await inventoryService.Received(1).UpdateSkuAsync(Arg.Is<Sku>(s => s.SkuId == "SKU-001" && s.ProductQuantity == 70));
        }
    }
}
