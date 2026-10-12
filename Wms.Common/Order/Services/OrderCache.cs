using System.Collections.Concurrent;
using Wms.Outbound.Order.Domain;
using Wms.Outbound.Order.Interfaces;

namespace Wms.Outbound.Order.Services
{
    public class OrderCache : IOrderCache
    {
        private readonly ConcurrentDictionary<string, CustomerOrder> _orders = new();

        public IReadOnlyDictionary<string, CustomerOrder> Orders => _orders;

        public void AddOrUpdate(CustomerOrder order)
        {
            _orders[order.OrderId] = order;
        }

        public Task<List<CustomerOrder>> GetCachedOrderListAsync()
        {
            return Task.FromResult(_orders.Values.ToList());
        }
    }
}
