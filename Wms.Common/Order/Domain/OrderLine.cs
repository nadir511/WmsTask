namespace Wms.Outbound.Order.Domain
{
    public class OrderLine
    {
        private readonly int _productId;

        public int LineId { get; }
        public string ProductId => $"P-{_productId}";
        public int Quantity { get; }
        public bool IsCancelled { get; private set; }

        public OrderLine(int id, string productId, int quantity)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(quantity, 1);
            LineId = id;
            _productId = int.Parse(productId);
            Quantity = quantity;
        }

        public void Cancel()
        {
            IsCancelled = true;
        }
    }
}
