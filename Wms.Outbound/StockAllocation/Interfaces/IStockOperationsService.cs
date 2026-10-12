using Wms.Outbound.Order.Domain;

namespace Wms.Outbound.StockAllocation.Interfaces
{
    public interface IStockOperationsService
    {
        /// <summary>
        /// Releases allocated SKU quantities for a cancelled order or cancelled
        /// line(s) and makes that stock immediately available to other orders.
        /// </summary>
        Task DeallocateOperationAsync(CustomerOrder order);
    }
}
