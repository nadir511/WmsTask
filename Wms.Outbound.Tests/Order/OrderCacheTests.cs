using Wms.Outbound.Order.Services;

namespace Wms.Outbound.Tests.Order
{
    public class OrderCacheTests
    {

        [Fact]
        public void AddOrUpdate_WithNewOrder_AddsItToOrders()
        {
            var cache = new OrderCache();
            var order = TestDataFactory.CreateDefaultOrder("ORD-001");

            cache.AddOrUpdate(order);

            Assert.Same(order, cache.Orders["ORD-001"]);
        }

        [Fact]
        public void AddOrUpdate_WithExistingOrderId_OverwritesTheStoredOrder()
        {
            var cache = new OrderCache();
            var original = TestDataFactory.CreateDefaultOrder("ORD-001");
            var updated = TestDataFactory.CreateDefaultOrder("ORD-001");

            cache.AddOrUpdate(original);
            cache.AddOrUpdate(updated);

            Assert.Single(cache.Orders);
            Assert.Same(updated, cache.Orders["ORD-001"]);
        }

        [Fact]
        public async Task GetCachedOrderListAsync_ReturnsAllAddedOrders()
        {
            var cache = new OrderCache();
            cache.AddOrUpdate(TestDataFactory.CreateDefaultOrder("ORD-001"));
            cache.AddOrUpdate(TestDataFactory.CreateDefaultOrder("ORD-002"));

            var orders = await cache.GetCachedOrderListAsync();

            Assert.Equal(2, orders.Count);
            Assert.Contains(orders, order => order.OrderId == "ORD-001");
            Assert.Contains(orders, order => order.OrderId == "ORD-002");
        }

        [Fact]
        public void AddOrUpdate_CalledConcurrently_AddsEveryOrderWithoutLosingAny()
        {
            var cache = new OrderCache();
            var orders = Enumerable.Range(1, 200)
                .Select(i => TestDataFactory.CreateDefaultOrder($"ORD-{i}"))
                .ToList();

            Parallel.ForEach(orders, cache.AddOrUpdate);

            Assert.Equal(200, cache.Orders.Count);
        }
    }
}
