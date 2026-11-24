using USAePayPortal.Models;
using USAePayPortal.Services;

namespace USAePayPortal.BackgroundServices;

/// <summary>
/// Charges every saved customer a fixed amount on a recurring interval.
/// </summary>
internal class RecurringPaymentService(
    IServiceScopeFactory scopeFactory,
    ILogger<RecurringPaymentService> logger) : BackgroundService
{
    private const decimal ChargeAmount = 1.00m;

    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan StartupDelay = TimeSpan.FromSeconds(10);

    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    private readonly ILogger<RecurringPaymentService> _logger = logger;

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Recurring payment service started – interval {Interval}", Interval);

        // Wait a short delay on startup so the app is fully initialised.
        await Task.Delay(StartupDelay, stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            await RunCycleAsync(stoppingToken);
            await Task.Delay(Interval, stoppingToken);
        }
    }

    private async Task RunCycleAsync(CancellationToken ct)
    {
        using IServiceScope scope = _scopeFactory.CreateScope();
        CustomerStoreSingleton store = scope.ServiceProvider.GetRequiredService<CustomerStoreSingleton>();
        IUSAePayService usaePay = scope.ServiceProvider.GetRequiredService<IUSAePayService>();

        IReadOnlyList<CustomerEntry> customers = store.GetAll();

        if (customers.Count == 0)
        {
            _logger.LogDebug("Recurring cycle: no customers to charge");
            return;
        }

        _logger.LogInformation(
            "Recurring cycle: charging {Count} customer(s) ${Amount}",
            customers.Count, ChargeAmount);

        foreach (CustomerEntry customer in customers)
        {
            if (ct.IsCancellationRequested)
            {
                break;
            }

            try
            {
                RecurringPaymentRecord record = await usaePay.ChargeCustomerAsync(
                    customer.CustomerKey, customer.PaymentMethodKey, ChargeAmount);

                store.RecordPayment(customer.CustomerKey, record);
                if (!record.Success)
                {
                    _logger.LogWarning("Scheduled recurring charge was not approved.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Recurring charge could not be confirmed ({ErrorType})", ex.GetType().Name);
            }
        }
    }
}
