public interface IResilientHttpClient
{
    Task<T> GetAsync<T>(string endpoint);
    Task<TResponse> PostAsync<TRequest, TResponse>(string endpoint, TRequest request);
}

public class ResilientHttpClient : IResilientHttpClient
{
    private readonly HttpClient _httpClient;
    private readonly ICircuitBreakerService _circuitBreaker;
    private readonly ILogger<ResilientHttpClient> _logger;

    public ResilientHttpClient(
        HttpClient httpClient, 
        ICircuitBreakerService circuitBreaker,
        ILogger<ResilientHttpClient> logger)
    {
        _httpClient = httpClient;
        _circuitBreaker = circuitBreaker;
        _logger = logger;
    }

    public async Task<T> GetAsync<T>(string endpoint)
    {
        return await _circuitBreaker.ExecuteAsync(async () =>
        {
            var response = await _httpClient.GetAsync(endpoint);
            response.EnsureSuccessStatusCode();
            
            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<T>(json);
        }, $"GET {endpoint}");
    }

    public async Task<TResponse> PostAsync<TRequest, TResponse>(string endpoint, TRequest request)
    {
        return await _circuitBreaker.ExecuteAsync(async () =>
        {
            var json = JsonSerializer.Serialize(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            
            var response = await _httpClient.PostAsync(endpoint, content);
            response.EnsureSuccessStatusCode();
            
            var responseJson = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<TResponse>(responseJson);
        }, $"POST {endpoint}");
    }
}