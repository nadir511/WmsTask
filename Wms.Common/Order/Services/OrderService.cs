using Wms.Outbound.Order.Domain;
using Wms.Outbound.Order.Interfaces;

namespace Wms.Outbound.Order.Services
{
    public class OrderService : IDisposable
    {
        private readonly IOrderRepository _orderRepository;
        private readonly List<CustomerOrder> _cachedOrders = [];
        public Action<CustomerOrder> NewOrderReceived { get; set; } = order => { };
        public OrderService(IOrderRepository orderRepository)
        {
            _orderRepository = orderRepository;
            SubscribeToNewOrders();
        }


        public async Task<List<CustomerOrder>> GetCachedOrderListAsync()
        {
            return await Task.FromResult(_cachedOrders);
        }

        public void Dispose()
        {
            _orderRepository.OrderReceived -= OnOrderReceived;
        }

        #region |Private Methods|

        private void SubscribeToNewOrders()
        {
            _orderRepository.OrderReceived += OnOrderReceived;
        }

        private void OnOrderReceived(CustomerOrder order)
        {
            _cachedOrders.Add(order);
            NewOrderReceived?.Invoke(order);
        }
        #endregion
    }
}
