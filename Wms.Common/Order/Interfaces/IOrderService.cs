using Wms.Outbound.Order.Domain;

namespace Wms.Outbound.Order.Interfaces
{
    public interface IOrderService
    {
        Action<CustomerOrder> NewOrderReceived { get; set; }
        Task<List<CustomerOrder>> GetCachedOrderListAsync();
        void OnOrderReceived(CustomerOrder order);
    }
}
