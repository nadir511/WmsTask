using Wms.Outbound.Order.Domain;

namespace Wms.Outbound.StockAllocation.Interfaces
{
    public interface IStockDeallocationService
    {
        /// <summary>
        /// Releases allocated SKU quantities for a cancelled order or cancelled
        /// line(s) and makes that stock immediately available to other orders.
        /// </summary>
        Task DeallocateAsync(CustomerOrder order);
    }
}
