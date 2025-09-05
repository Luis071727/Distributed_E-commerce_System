public class OrderSagaState
{
    public Guid OrderId { get; set; }
    public Guid CorrelationId { get; set; }
    public SagaStep CurrentStep { get; set; }
    public bool InventoryReserved { get; set; }
    public bool PaymentProcessed { get; set; }
    public bool OrderConfirmed { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public List<CompensationAction> CompensationActions { get; set; } = new();
    
    public void AddCompensationAction(CompensationAction action)
    {
        CompensationActions.Add(action);
    }
}

public enum SagaStep
{
    OrderCreated,
    InventoryReservation,
    PaymentProcessing,
    OrderConfirmation,
    Completed,
    Failed,
    Compensating
}

public class CompensationAction
{
    public string ActionType { get; set; }
    public Dictionary<string, object> Parameters { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool Executed { get; set; }
}