namespace Wms.Outbound.Order.Domain;

public class CustomerOrder
{
    private readonly List<OrderLine> _lines;

    public CustomerOrder(string orderId, string customerId, IEnumerable<OrderLine> lines, OrderPriority priority, DateTime orderDate, bool completeDeliveryRequired = false)
    {
        OrderId = orderId;
        CustomerId = customerId;
        _lines = lines.ToList();
        Priority = priority;
        OrderDate = orderDate;
        CompleteDeliveryRequired = completeDeliveryRequired;
        Status = OrderStatus.Released;
    }

    public string OrderId { get; }
    public string CustomerId { get; }
    public IReadOnlyList<OrderLine> Lines => _lines;
    public bool CompleteDeliveryRequired { get; }
    public OrderPriority Priority { get; }
    public OrderStatus Status { get; private set; }
    public DateTime OrderDate { get; }
    public bool IsCancled { get; private set; }

    public void AddLine(OrderLine line)
    {
        if (_lines.Any(existing => existing.LineId == line.LineId))
            throw new InvalidOperationException($"Line {line.LineId} already exists on order {OrderId}.");

        _lines.Add(line);
    }

    public void CancelWholeOrder()
    {
        IsCancled = true;
    }

    public void CancelLine(int lineId)
    {
        var line = _lines.SingleOrDefault(l => l.LineId == lineId)
            ?? throw new InvalidOperationException($"Line {lineId} not found on order {OrderId}.");

        line.Cancel();
    }

    public void MarkCancelled()
    {
        Status = OrderStatus.Cancelled;
    }

    public void RecalculateStatus(Func<OrderLine, int> getAllocatedQuantity)
    {
        var activeLines = _lines.Where(line => !line.IsCancelled).ToList();
        var allLinesFulfilled = activeLines.Count == 0 ||
            activeLines.All(line => getAllocatedQuantity(line) >= line.Quantity);

        if (allLinesFulfilled)
            Status = OrderStatus.ReadyForDelivery;
        else if (activeLines.Any(line => getAllocatedQuantity(line) > 0))
            Status = OrderStatus.PartiallyAllocated;
        else
            Status = OrderStatus.Released;
    }
}
