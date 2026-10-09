namespace Wms.Outbound.Order;

public class CustomerOrder
{
    public required string OrderId { get; set; }
    public required string CustomerId { get; set; }
    public List<OrderLine> OrderLines { get; set; } = default!;
    public bool CompleteDeliveryRequired { get; set; }
    public OrderPeriority OrderPeriority { get; set; }
    public DateTime OrderDate { get; set; }

}
public enum OrderPeriority
{
    Low,
    Normal,
    Heigh
}