public abstract class DomainEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime OccurredOn { get; set; } = DateTime.UtcNow;
    public string CorrelationId { get; set; }
}

public class OrderCreatedEvent : DomainEvent
{
    public Guid OrderId { get; set; }
    public Guid CustomerId { get; set; }
    public List<OrderItem> Items { get; set; }
    public decimal TotalAmount { get; set; }
}

public class InventoryReservedEvent : DomainEvent
{
    public Guid OrderId { get; set; }
    public List<InventoryItem> ReservedItems { get; set; }
    public DateTime ReservationExpiry { get; set; }
}

public class PaymentProcessedEvent : DomainEvent
{
    public Guid OrderId { get; set; }
    public Guid PaymentId { get; set; }
    public decimal Amount { get; set; }
    public bool IsSuccessful { get; set; }
}

public class OrderCompletedEvent : DomainEvent
{
    public Guid OrderId { get; set; }
    public DateTime CompletedAt { get; set; }
}