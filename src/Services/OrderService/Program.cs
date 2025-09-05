var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddControllers();
builder.Services.AddDbContext<OrderDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Add custom services
builder.Services.AddServiceDiscovery(builder.Configuration);
builder.Services.AddMessageBus(builder.Configuration);
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddScoped<IOrderSagaOrchestrator, OrderSagaOrchestrator>();

// Add health checks
builder.Services.AddHealthChecks()
    .AddDbContext<OrderDbContext>();

var app = builder.Build();

// Configure pipeline
app.UseRouting();
app.MapControllers();
app.MapHealthChecks("/health");

await app.RunAsync();