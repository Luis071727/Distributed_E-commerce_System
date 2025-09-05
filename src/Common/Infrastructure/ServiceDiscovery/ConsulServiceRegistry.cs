public interface IServiceRegistry
{
    Task RegisterServiceAsync(ServiceRegistration registration);
    Task<List<ServiceInstance>> DiscoverServicesAsync(string serviceName);
    Task DeregisterServiceAsync(string serviceId);
}

public class ConsulServiceRegistry : IServiceRegistry
{
    private readonly IConsulClient _consulClient;

    public ConsulServiceRegistry(IConsulClient consulClient)
    {
        _consulClient = consulClient;
    }

    public async Task RegisterServiceAsync(ServiceRegistration registration)
    {
        var agentServiceRegistration = new AgentServiceRegistration
        {
            ID = registration.Id,
            Name = registration.Name,
            Address = registration.Address,
            Port = registration.Port,
            Tags = registration.Tags.ToArray(),
            Check = new AgentServiceCheck
            {
                HTTP = $"http://{registration.Address}:{registration.Port}/health",
                Interval = TimeSpan.FromSeconds(10),
                Timeout = TimeSpan.FromSeconds(5)
            }
        };

        await _consulClient.Agent.ServiceRegister(agentServiceRegistration);
    }

    public async Task<List<ServiceInstance>> DiscoverServicesAsync(string serviceName)
    {
        var services = await _consulClient.Health.Service(serviceName, string.Empty, true);
        return services.Response.Select(s => new ServiceInstance
        {
            Id = s.Service.ID,
            Name = s.Service.Service,
            Address = s.Service.Address,
            Port = s.Service.Port
        }).ToList();
    }

    public async Task DeregisterServiceAsync(string serviceId)
    {
        await _consulClient.Agent.ServiceDeregister(serviceId);
    }
}