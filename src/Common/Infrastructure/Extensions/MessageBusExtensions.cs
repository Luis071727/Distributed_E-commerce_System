public static class MessageBusExtensions
{
    public static IServiceCollection AddMessageBus(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("ServiceBus");
        
        services.AddSingleton(new ServiceBusClient(connectionString));
        services.AddSingleton<IMessageBus, ServiceBusMessageBus>();
        
        return services;
    }
}