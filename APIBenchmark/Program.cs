using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace APIBenchmark;

class Program
{
    static async Task Main(string[] args)
    {
        // Build configuration from the appsettings.json file
        var builder = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

        IConfiguration configuration = builder.Build();

        // Configure services with dependency injection
        var services = new ServiceCollection();
        services.Configure<Options>(opts =>
        {
            configuration.GetSection("Options").Bind(opts);
        });
        services.AddScoped<IApiLatency, ApiLatency>();

        var serviceProvider = services.BuildServiceProvider();

        // Get an instance of ApiLatencyChecker with injected data
        var apiLatency = serviceProvider.GetRequiredService<IApiLatency>();

        // Start the latency measurement
        await apiLatency.Process();
    }
}
