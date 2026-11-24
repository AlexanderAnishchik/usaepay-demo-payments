using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using USAePayPortal.Controllers;
using USAePayPortal.Services;

namespace USAePayPortal.Tests;

// Run the real ASP.NET pipeline in memory. Only the outgoing gateway transport
// and the background timer are replaced; tests trigger recurring charges explicitly.
internal sealed class PortalFactory : WebApplicationFactory<PaymentController>
{
    public ScriptedGateway Gateway { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            services.PostConfigure<USAePayOptions>(options =>
            {
                options.BaseUrl = "https://gateway.invalid/api/v2";
                options.ApiKey = "test-api-key";
                options.Pin = "test-pin";
                options.PublicApiKey = "test-public-key";
            });
            services.ConfigureHttpClientDefaults(client => client.ConfigurePrimaryHttpMessageHandler(() => Gateway));

            var billingWorker = services.Single(service => service.ServiceType == typeof(IHostedService)
                && service.ImplementationType?.Assembly == typeof(PaymentController).Assembly);
            services.Remove(billingWorker);
        });
    }
}

internal sealed class ScriptedGateway : HttpMessageHandler
{
    private readonly Queue<Func<HttpResponseMessage>> _responses = new();
    public List<(string Path, JsonElement Body)> Requests { get; } = new();

    public void Reply(string json, HttpStatusCode status = HttpStatusCode.OK) =>
        _responses.Enqueue(() => new HttpResponseMessage(status)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        });

    public void Timeout() =>
        _responses.Enqueue(() => throw new TaskCanceledException("Simulated gateway timeout"));

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
    {
        if (request.Method != HttpMethod.Post || request.RequestUri?.Host != "gateway.invalid")
            throw new InvalidOperationException("Unexpected gateway request");

        var body = await request.Content!.ReadFromJsonAsync<JsonElement>(token);
        Requests.Add((request.RequestUri.AbsolutePath, body));
        return _responses.Dequeue()();
    }
}
