using Wms.Outbound.Inventory.Domain;

namespace Wms.Outbound.Inventory.Services
{
    public interface IInventoryRepository
    {
        public Task<Sku> GetSkuByIdAsync(string skuId);
        public Task<IEnumerable<Sku>> GetSkuListAsync();
        public Task<bool> UpdateSkuAsync(Sku sku);
    }
}
