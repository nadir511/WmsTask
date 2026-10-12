using Wms.Outbound.Inventory;

namespace Wms.Outbound.Order.Domain
{
    public class OrderLine
    {
        public int LineId { get; set; }
        private int _productId;
        public string ProductId { get => $"P-{_productId}"; set => _productId = int.Parse(value); }
        public int Quantity { get; set; }
        public OrderLine(int id, string productId, int quantity)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(quantity, 1);
            LineId = id;
            ProductId = productId;
            Quantity = quantity;
        }
    }
}
