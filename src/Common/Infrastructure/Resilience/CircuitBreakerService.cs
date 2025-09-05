public interface ICircuitBreakerService
{
    Task<T> ExecuteAsync<T>(Func<Task<T>> operation, string operationName);
    Task ExecuteAsync(Func<Task> operation, string operationName);
}

public class CircuitBreakerService : ICircuitBreakerService
{
    private readonly IAsyncPolicy _circuitBreakerPolicy;
    private readonly ILogger<CircuitBreakerService> _logger;

    public CircuitBreakerService(ILogger<CircuitBreakerService> logger)
    {
        _logger = logger;
        
        _circuitBreakerPolicy = Policy
            .Handle<HttpRequestException>()
            .Or<TaskCanceledException>()
            .Or<SocketException>()
            .CircuitBreakerAsync(
                handledEventsAllowedBeforeBreaking: 3,
                durationOfBreak: TimeSpan.FromSeconds(30),
                onBreak: (exception, duration) =>
                {
                    _logger.LogWarning($"Circuit breaker opened for {duration.TotalSeconds} seconds due to: {exception.Message}");
                },
                onReset: () =>
                {
                    _logger.LogInformation("Circuit breaker reset - service calls will be allowed");
                },
                onHalfOpen: () =>
                {
                    _logger.LogInformation("Circuit breaker half-open - testing service availability");
                });
    }

    public async Task<T> ExecuteAsync<T>(Func<Task<T>> operation, string operationName)
    {
        var retryPolicy = Policy
            .Handle<HttpRequestException>()
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                onRetry: (outcome, delay, retryCount, context) =>
                {
                    _logger.LogWarning($"Retry {retryCount} for {operationName} in {delay.TotalSeconds} seconds");
                });

        var combinedPolicy = Policy.WrapAsync(_circuitBreakerPolicy, retryPolicy);

        return await combinedPolicy.ExecuteAsync(async () =>
        {
            _logger.LogDebug($"Executing {operationName}");
            return await operation();
        });
    }

    public async Task ExecuteAsync(Func<Task> operation, string operationName)
    {
        await ExecuteAsync(async () =>
        {
            await operation();
            return true;
        }, operationName);
    }
}