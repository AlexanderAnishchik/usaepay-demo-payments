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

        // HTTP client
        services.AddHttpClient<USAePayService>();

        // Application services
        services.AddSingleton<CustomerStoreSingleton>();
        services.AddScoped<IUSAePayService, USAePayService>();

        // Background services
        services.AddHostedService<RecurringPaymentService>();

        // ASP.NET Core infrastructure
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
