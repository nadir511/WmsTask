using Wms.Outbound.Inventory;

namespace Wms.Outbound.Order
{
    public class OrderLine
    {
        public int Id { get; set; }
        private int _productId;

        public string ProductId
        {
            get => $"P-{_productId}";
            set
            {
                _productId = int.Parse(value);
            }
        }
        public required int Quantity { get; set; }
        public OrderLine(int id, string productId, int quantity)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(quantity, 1);
            Id = id;
            ProductId = productId;
            Quantity = quantity;
        }
    }
}
