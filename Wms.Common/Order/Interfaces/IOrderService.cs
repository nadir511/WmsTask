using System.Threading.Channels;
using Wms.Outbound.Order.Domain;

namespace Wms.Outbound.Order.Interfaces
{
    public interface IOrderService
    {
        ChannelReader<CustomerOrder> NewOrders { get; }
        ChannelReader<CustomerOrder> Cancellations { get; }
        void OnOrderReceived(CustomerOrder order);
        void OnOrderOrLineCancellationRequest(CustomerOrder order);
    }
}
