public interface IMessageBus
{
    Task PublishAsync<T>(T message) where T : class;
    Task SubscribeAsync<T>(Func<T, Task> handler) where T : class;
}

public class ServiceBusMessageBus : IMessageBus
{
    private readonly ServiceBusClient _client;
    private readonly ILogger<ServiceBusMessageBus> _logger;
    private readonly Dictionary<Type, ServiceBusSender> _senders;
    private readonly Dictionary<Type, ServiceBusProcessor> _processors;

    public ServiceBusMessageBus(ServiceBusClient client, ILogger<ServiceBusMessageBus> logger)
    {
        _client = client;
        _logger = logger;
        _senders = new Dictionary<Type, ServiceBusSender>();
        _processors = new Dictionary<Type, ServiceBusProcessor>();
    }

    public async Task PublishAsync<T>(T message) where T : class
    {
        var topicName = GetTopicName<T>();
        
        if (!_senders.ContainsKey(typeof(T)))
        {
            _senders[typeof(T)] = _client.CreateSender(topicName);
        }

        var sender = _senders[typeof(T)];
        var json = JsonSerializer.Serialize(message);
        var serviceBusMessage = new ServiceBusMessage(json)
        {
            ContentType = "application/json",
            Subject = typeof(T).Name
        };

        // Add correlation ID for tracing
        if (message is DomainEvent domainEvent)
        {
            serviceBusMessage.CorrelationId = domainEvent.CorrelationId;
        }

        await sender.SendMessageAsync(serviceBusMessage);
        _logger.LogInformation($"Published message of type {typeof(T).Name}");
    }

    public async Task SubscribeAsync<T>(Func<T, Task> handler) where T : class
    {
        var topicName = GetTopicName<T>();
        var subscriptionName = $"{Environment.MachineName}-{typeof(T).Name}";
        
        var processor = _client.CreateProcessor(topicName, subscriptionName, new ServiceBusProcessorOptions
        {
            AutoCompleteMessages = false,
            MaxConcurrentCalls = 1
        });

        processor.ProcessMessageAsync += async (args) =>
        {
            try
            {
                var json = args.Message.Body.ToString();
                var message = JsonSerializer.Deserialize<T>(json);
                
                await handler(message);
                await args.CompleteMessageAsync(args.Message);
                
                _logger.LogInformation($"Processed message of type {typeof(T).Name}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error processing message of type {typeof(T).Name}");
                await args.AbandonMessageAsync(args.Message);
            }
        };

        processor.ProcessErrorAsync += (args) =>
        {
            _logger.LogError(args.Exception, $"Error in message processor for {typeof(T).Name}");
            return Task.CompletedTask;
        };

        _processors[typeof(T)] = processor;
        await processor.StartProcessingAsync();
    }

    private string GetTopicName<T>() => typeof(T).Name.ToLowerInvariant();
}