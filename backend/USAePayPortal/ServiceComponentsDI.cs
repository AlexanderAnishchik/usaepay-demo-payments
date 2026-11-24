using USAePayPortal.BackgroundServices;
using USAePayPortal.Services;

namespace USAePayPortal;

/// <summary>
/// Extension methods for registering all application services in the DI container.
/// </summary>
internal static class ServiceComponentsDI
{
    internal const string FrontendCorsPolicy = "FrontendPolicy";

    /// <summary>
    /// Registers configuration, HTTP clients, application services, background services,
    /// API infrastructure, and CORS policy.
    /// </summary>
    internal static IServiceCollection AddApplicationServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        string frontendUrl = configuration["USAePay:FrontendUrl"] ?? "http://localhost:5173";

        // Configuration
        services.Configure<USAePayOptions>(configuration.GetSection("USAePay"));

        // The vault URL contains the customer key. Log operation names and status in the service.
        services.AddHttpClient<USAePayService>().RemoveAllLoggers();

        // Application services
        services.AddSingleton<CustomerStoreSingleton>();
        services.AddScoped<IUSAePayService>(provider => provider.GetRequiredService<USAePayService>());

        // Background services
        services.AddHostedService<RecurringPaymentService>();

        // ASP.NET Core infrastructure
        // The local DELETE route also carries a customer key in the request path.
        services.AddLogging(logging => logging.AddFilter("Microsoft.AspNetCore.Hosting.Diagnostics", LogLevel.Warning));
        services.AddControllers();
        services.AddOpenApi();

        // CORS
        services.AddCors(options =>
            options.AddPolicy(FrontendCorsPolicy, policy =>
                policy.WithOrigins(frontendUrl)
                      .AllowAnyHeader()
                      .AllowAnyMethod()));

        return services;
    }
}
