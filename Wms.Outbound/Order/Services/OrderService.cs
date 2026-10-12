using System.Threading.Channels;
using Wms.Outbound.Order.Domain;
using Wms.Outbound.Order.Interfaces;

namespace Wms.Outbound.Order.Services
{
    public class OrderService : IOrderService, IDisposable
    {
        private readonly IOrderRepository _orderRepository;
        private readonly IOrderCache _orderCache;
        private readonly Channel<CustomerOrder> _newOrders = Channel.CreateUnbounded<CustomerOrder>();
        private readonly Channel<CustomerOrder> _ordercancellations = Channel.CreateUnbounded<CustomerOrder>();

        public ChannelReader<CustomerOrder> NewOrders => _newOrders.Reader;
        public ChannelReader<CustomerOrder> Cancellations => _ordercancellations.Reader;


        public OrderService(IOrderRepository orderRepository, IOrderCache orderCache)
        {
            _orderRepository = orderRepository;
            _orderCache = orderCache;
            SubscribeToNewOrders();
            SubscribeForOrderCancellationRequests();
        }
        public void OnOrderReceived(CustomerOrder order)
        {
            _orderCache.AddOrUpdate(order);
            _newOrders.Writer.TryWrite(order);
        }
        public void OnOrderOrLineCancellationRequest(CustomerOrder order)
        {
            _orderCache.AddOrUpdate(order);
            _ordercancellations.Writer.TryWrite(order);
        }
        public void Dispose()
        {
            _orderRepository.OrderReceived -= OnOrderReceived;
            _orderRepository.OrderOrLineCancellationRequest -= OnOrderOrLineCancellationRequest;
        }

        #region |Private Methods|

        private void SubscribeToNewOrders()
        {
            _orderRepository.OrderReceived += OnOrderReceived;
        }
        private void SubscribeForOrderCancellationRequests()
        {
            _orderRepository.OrderOrLineCancellationRequest += OnOrderOrLineCancellationRequest;
        }

        #endregion
    }
}
