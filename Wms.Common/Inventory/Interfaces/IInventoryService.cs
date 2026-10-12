using System;
using System.Collections.Generic;
using System.Text;
using Wms.Outbound.Inventory.Domain;

namespace Wms.Outbound.Inventory.Interfaces
{
    public interface IInventoryService
    {
        Task<Sku> GetSkuByIdAsync(string skuId);
        Task<IEnumerable<Sku>> GetSkuListAsync();
        Task<bool> UpdateSkuAsync(Sku sku);
    }
}
