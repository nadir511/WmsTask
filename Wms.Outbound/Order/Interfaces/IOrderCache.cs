using Wms.Outbound.Order.Domain;

namespace Wms.Outbound.Order.Interfaces
{
    public interface IOrderCache
    {
        IReadOnlyDictionary<string, CustomerOrder> Orders { get; }
        void AddOrUpdate(CustomerOrder order);
        Task<List<CustomerOrder>> GetCachedOrderListAsync();
    }
}
