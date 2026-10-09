namespace Wms.Outbound.Order;

public class CustomerOrder
{
    public required string OrderId { get; set; }
    public required string CustomerId { get; set; }
    public List<OrderLine> Lines { get; set; } = default!;
    public bool CompleteDeliveryRequired { get; set; }
    public OrderPriority Priority { get; set; }
    public OrderStatus Status { get; set; }
    public DateTime OrderDate { get; set; }

}
public enum OrderPriority
{
    Low,
    Normal,
    High
}
public enum OrderStatus
{
    Released,
    PartiallyAllocated,
    ReadyForDelivery
}