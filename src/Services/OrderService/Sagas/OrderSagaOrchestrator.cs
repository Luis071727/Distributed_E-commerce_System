public interface IOrderSagaOrchestrator
{
    Task StartSagaAsync(Order order);
    Task HandleInventoryReservedAsync(InventoryReservedEvent @event);
    Task HandlePaymentProcessedAsync(PaymentProcessedEvent @event);
    Task HandleInventoryReservationFailedAsync(InventoryReservationFailedEvent @event);
    Task HandlePaymentFailedAsync(PaymentFailedEvent @event);
}

public class OrderSagaOrchestrator : IOrderSagaOrchestrator
{
    private readonly IMessageBus _messageBus;
    private readonly ISagaStateRepository _sagaStateRepository;
    private readonly ILogger<OrderSagaOrchestrator> _logger;

    public OrderSagaOrchestrator(
        IMessageBus messageBus, 
        ISagaStateRepository sagaStateRepository,
        ILogger<OrderSagaOrchestrator> logger)
    {
        _messageBus = messageBus;
        _sagaStateRepository = sagaStateRepository;
        _logger = logger;
    }

    public async Task StartSagaAsync(Order order)
    {
        var sagaState = new OrderSagaState
        {
            OrderId = order.Id,
            CorrelationId = Guid.NewGuid(),
            CurrentStep = SagaStep.OrderCreated,
            CreatedAt = DateTime.UtcNow
        };

        await _sagaStateRepository.SaveAsync(sagaState);

        // Step 1: Reserve Inventory
        await ReserveInventoryAsync(sagaState, order);
    }

    private async Task ReserveInventoryAsync(OrderSagaState sagaState, Order order)
    {
        try
        {
            sagaState.CurrentStep = SagaStep.InventoryReservation;
            await _sagaStateRepository.SaveAsync(sagaState);

            var reserveInventoryCommand = new ReserveInventoryCommand
            {
                OrderId = order.Id,
                CorrelationId = sagaState.CorrelationId,
                Items = order.Items.Select(i => new InventoryItem
                {
                    ProductId = i.ProductId,
                    Quantity = i.Quantity
                }).ToList()
            };

            // Add compensation action
            sagaState.AddCompensationAction(new CompensationAction
            {
                ActionType = "ReleaseInventory",
                Parameters = new Dictionary<string, object>
                {
                    ["OrderId"] = order.Id,
                    ["Items"] = order.Items
                },
                CreatedAt = DateTime.UtcNow
            });

            await _messageBus.PublishAsync(reserveInventoryCommand);
            
            _logger.LogInformation($"Inventory reservation requested for order {order.Id}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error reserving inventory for order {order.Id}");
            await CompensateTransactionAsync(sagaState);
        }
    }

    public async Task HandleInventoryReservedAsync(InventoryReservedEvent @event)
    {
        var sagaState = await _sagaStateRepository.GetByCorrelationIdAsync(@event.CorrelationId);
        if (sagaState == null) return;

        sagaState.InventoryReserved = true;
        await _sagaStateRepository.SaveAsync(sagaState);

        // Step 2: Process Payment
        await ProcessPaymentAsync(sagaState);
    }

    private async Task ProcessPaymentAsync(OrderSagaState sagaState)
    {
        try
        {
            sagaState.CurrentStep = SagaStep.PaymentProcessing;
            await _sagaStateRepository.SaveAsync(sagaState);

            var order = await GetOrderAsync(sagaState.OrderId);
            
            var processPaymentCommand = new ProcessPaymentCommand
            {
                OrderId = order.Id,
                CorrelationId = sagaState.CorrelationId,
                Amount = order.TotalAmount,
                CustomerId = order.CustomerId
            };

            // Add compensation action
            sagaState.AddCompensationAction(new CompensationAction
            {
                ActionType = "RefundPayment",
                Parameters = new Dictionary<string, object>
                {
                    ["OrderId"] = order.Id,
                    ["Amount"] = order.TotalAmount
                },
                CreatedAt = DateTime.UtcNow
            });

            await _messageBus.PublishAsync(processPaymentCommand);
            
            _logger.LogInformation($"Payment processing requested for order {order.Id}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error processing payment for order {sagaState.OrderId}");
            await CompensateTransactionAsync(sagaState);
        }
    }

    public async Task HandlePaymentProcessedAsync(PaymentProcessedEvent @event)
    {
        var sagaState = await _sagaStateRepository.GetByCorrelationIdAsync(@event.CorrelationId);
        if (sagaState == null) return;

        if (@event.IsSuccessful)
        {
            sagaState.PaymentProcessed = true;
            await _sagaStateRepository.SaveAsync(sagaState);

            // Step 3: Confirm Order
            await ConfirmOrderAsync(sagaState);
        }
        else
        {
            await CompensateTransactionAsync(sagaState);
        }
    }

    private async Task ConfirmOrderAsync(OrderSagaState sagaState)
    {
        try
        {
            sagaState.CurrentStep = SagaStep.OrderConfirmation;
            sagaState.OrderConfirmed = true;
            sagaState.CurrentStep = SagaStep.Completed;
            sagaState.CompletedAt = DateTime.UtcNow;
            
            await _sagaStateRepository.SaveAsync(sagaState);

            var orderCompletedEvent = new OrderCompletedEvent
            {
                OrderId = sagaState.OrderId,
                CorrelationId = sagaState.CorrelationId,
                CompletedAt = DateTime.UtcNow
            };

            await _messageBus.PublishAsync(orderCompletedEvent);
            
            _logger.LogInformation($"Order {sagaState.OrderId} completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error confirming order {sagaState.OrderId}");
            await CompensateTransactionAsync(sagaState);
        }
    }

    private async Task CompensateTransactionAsync(OrderSagaState sagaState)
    {
        _logger.LogWarning($"Starting compensation for order {sagaState.OrderId}");
        
        sagaState.CurrentStep = SagaStep.Compensating;
        await _sagaStateRepository.SaveAsync(sagaState);

        // Execute compensation actions in reverse order
        var compensationActions = sagaState.CompensationActions
            .OrderByDescending(a => a.CreatedAt)
            .Where(a => !a.Executed)
            .ToList();

        foreach (var action in compensationActions)
        {
            try
            {
                await ExecuteCompensationActionAsync(action, sagaState);
                action.Executed = true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to execute compensation action {action.ActionType}");
            }
        }

        sagaState.CurrentStep = SagaStep.Failed;
        await _sagaStateRepository.SaveAsync(sagaState);
    }

    private async Task ExecuteCompensationActionAsync(CompensationAction action, OrderSagaState sagaState)
    {
        switch (action.ActionType)
        {
            case "ReleaseInventory":
                var releaseInventoryCommand = new ReleaseInventoryCommand
                {
                    OrderId = (Guid)action.Parameters["OrderId"],
                    CorrelationId = sagaState.CorrelationId
                };
                await _messageBus.PublishAsync(releaseInventoryCommand);
                break;

            case "RefundPayment":
                var refundPaymentCommand = new RefundPaymentCommand
                {
                    OrderId = (Guid)action.Parameters["OrderId"],
                    Amount = (decimal)action.Parameters["Amount"],
                    CorrelationId = sagaState.CorrelationId
                };
                await _messageBus.PublishAsync(refundPaymentCommand);
                break;
        }
    }
}