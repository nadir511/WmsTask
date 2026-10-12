using NSubstitute;
using Wms.Outbound.Order.Interfaces;
using Wms.Outbound.Order.Services;

namespace Wms.Outbound.Tests.Order
{
    public class OrderServiceTests
    {
        [Fact]
        public void OnOrderReceived_AddsOrderToCacheAndWritesToNewOrdersChannel()
        {
            // Arrange
            var orderRepository = Substitute.For<IOrderRepository>();
            var orderCache = new OrderCache();
            var orderService = new OrderService(orderRepository, orderCache);
            var order = TestDataFactory.CreateDefaultOrder("ORD-001");

            // Act
            orderService.OnOrderReceived(order);

            // Assert
            Assert.Same(order, orderCache.Orders["ORD-001"]);
            Assert.True(orderService.NewOrders.TryRead(out var written));
            Assert.Same(order, written);
        }

        [Fact]
        public void OnOrderOrLineCancellationRequest_AddsOrderToCacheAndWritesToCancellationsChannel()
        {
            // Arrange
            var orderRepository = Substitute.For<IOrderRepository>();
            var orderCache = new OrderCache();
            var orderService = new OrderService(orderRepository, orderCache);
            var order = TestDataFactory.CreateDefaultOrder("ORD-001");
            order.CancelWholeOrder();

            // Act
            orderService.OnOrderOrLineCancellationRequest(order);

            // Assert
            Assert.Same(order, orderCache.Orders["ORD-001"]);
            Assert.True(orderService.Cancellations.TryRead(out var written));
            Assert.Same(order, written);
        }

        [Fact]
        public void Constructor_SubscribesToOrderReceivedEvent_RaisingEventAddsOrderToCache()
        {
            // Arrange
            var orderRepository = Substitute.For<IOrderRepository>();
            var orderCache = new OrderCache();
            _ = new OrderService(orderRepository, orderCache);
            var order = TestDataFactory.CreateDefaultOrder("ORD-001");

            // Act
            orderRepository.OrderReceived += Raise.Event<Action<Outbound.Order.Domain.CustomerOrder>>(order);

            // Assert
            Assert.Same(order, orderCache.Orders["ORD-001"]);
        }

        [Fact]
        public void Constructor_SubscribesToCancellationEvent_RaisingEventAddsOrderToCache()
        {
            // Arrange
            var orderRepository = Substitute.For<IOrderRepository>();
            var orderCache = new OrderCache();
            _ = new OrderService(orderRepository, orderCache);
            var order = TestDataFactory.CreateDefaultOrder("ORD-001");

            // Act
            orderRepository.OrderOrLineCancellationRequest += Raise.Event<Action<Outbound.Order.Domain.CustomerOrder>>(order);

            // Assert
            Assert.Same(order, orderCache.Orders["ORD-001"]);
        }

        [Fact]
        public void Dispose_UnsubscribesFromBothEvents_SoSubsequentEventsAreIgnored()
        {
            // Arrange
            var orderRepository = Substitute.For<IOrderRepository>();
            var orderCache = new OrderCache();
            var orderService = new OrderService(orderRepository, orderCache);

            // Act
            orderService.Dispose();
            var order = TestDataFactory.CreateDefaultOrder("ORD-001");
            orderRepository.OrderReceived += Raise.Event<Action<Outbound.Order.Domain.CustomerOrder>>(order);
            orderRepository.OrderOrLineCancellationRequest += Raise.Event<Action<Outbound.Order.Domain.CustomerOrder>>(order);

            // Assert
            Assert.Empty(orderCache.Orders);
        }
    }
}
