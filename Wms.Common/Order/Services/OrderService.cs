using Wms.Outbound.Order.Domain;
using Wms.Outbound.Order.Interfaces;

namespace Wms.Outbound.Order.Services
{
    public class OrderService : IOrderService, IDisposable
    {
        private readonly IOrderRepository _orderRepository;
        private readonly List<CustomerOrder> _cachedOrders;
        public Action<CustomerOrder> NewOrderReceived { get; set; } = order => { };
        public OrderService(IOrderRepository orderRepository)
        {
            _orderRepository = orderRepository;
            _cachedOrders = [];
            SubscribeToNewOrders();
        }


        public async Task<List<CustomerOrder>> GetCachedOrderListAsync()
        {
            var orders = _cachedOrders.Where(x => x.Status != OrderStatus.ReadyForDelivery).ToList();
            return orders;
        }
        public void OnOrderReceived(CustomerOrder order)
        {
            _cachedOrders.Add(order);
            NewOrderReceived?.Invoke(order);
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
       
        #endregion
    }
}
