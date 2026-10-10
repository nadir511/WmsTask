namespace Wms.Outbound.StockAllocation.Domain
{
    public sealed record OrderLineAllocation(string OrderId, int LineId, string SkuId, int Quantity);
}
