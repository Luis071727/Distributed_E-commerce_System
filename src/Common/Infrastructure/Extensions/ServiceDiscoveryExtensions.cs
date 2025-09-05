public static class ServiceDiscoveryExtensions
{
    public static IServiceCollection AddServiceDiscovery(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<ConsulConfig>(configuration.GetSection("Consul"));
        services.AddSingleton<IConsulClient, ConsulClient>(p => new ConsulClient(consulConfig =>
        {
            var address = configuration["Consul:Host"];
            consulConfig.Address = new Uri(address);
        }));
        
        services.AddSingleton<IServiceRegistry, ConsulServiceRegistry>();
        services.AddHostedService<ConsulHostedService>();
        
        return services;
    }
}