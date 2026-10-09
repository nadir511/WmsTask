using System;
using System.Collections.Generic;
using System.Text;

namespace Wms.Outbound.StockAllocation
{
    public sealed record StockAllocation(string OrderId, int LineId, string SkuId, int Quantity);
}
