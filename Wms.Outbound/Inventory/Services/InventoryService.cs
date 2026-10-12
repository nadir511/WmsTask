using Wms.Outbound.Inventory.Domain;
using Wms.Outbound.Inventory.Interfaces;

namespace Wms.Outbound.Inventory.Services
{
    public class InventoryService : IInventoryService
    {
        private readonly IInventoryRepository _inventoryRepository;

        public InventoryService(IInventoryRepository inventoryRepository)
        {
            _inventoryRepository = inventoryRepository;
        }


        public async Task<Sku> GetSkuByIdAsync(string skuId)
        {
            return await _inventoryRepository.GetSkuByIdAsync(skuId);
        }

        public async Task<IEnumerable<Sku>> GetSkuListAsync()
        {
            return await _inventoryRepository.GetSkuListAsync();
        }

        public async Task<bool> UpdateSkuAsync(Sku sku)
        {
            return await _inventoryRepository.UpdateSkuAsync(sku);
        }
    }
}
