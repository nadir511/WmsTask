using Wms.Outbound.Order.Domain;

namespace Wms.Outbound.Order.Interfaces
{
    public interface IOrderRepository
    {
        event Action<CustomerOrder> OrderReceived;
    }
}
