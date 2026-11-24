using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace USAePayPortal.Tests;

public class PaymentFlowTests
{
    private const string Customer = """{"key":"customer-1"}""";
    private const string Approval = """{"result_code":"A","result":"Approved","key":"sale-1","authcode":"auth-1"}""";
    private const string Decline = """{"result_code":"D","result":"Declined"}""";

    private static readonly object SaveRequest = new
    {
        paymentKey = "single-use-token", firstName = "Sample", lastName = "Customer",
        email = "sample@example.test", chargeAmount = 10.00m
    };

    [Fact]
    public async Task DocumentedOpenApiUrlIncludesTheSaveCustomerEndpoint()
    {
        using var app = new PortalFactory();
        using var client = app.CreateClient();

        var document = await client.GetFromJsonAsync<JsonElement>("/openapi/v1.json");

        Assert.True(document.GetProperty("paths").GetProperty("/api/payment/customers").TryGetProperty("post", out _));
        Assert.Empty(app.Gateway.Requests);
    }

    [Fact]
    public async Task OneOffChargeTranslatesTheBrowserTokenIntoAGatewaySale()
    {
        using var app = new PortalFactory();
        using var client = app.CreateClient();
        app.Gateway.Reply(Approval);

        using var response = await client.PostAsJsonAsync("/api/payment/charge",
            new { paymentKey = "single-use-token", amount = 10.00m });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var receipt = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(receipt.GetProperty("success").GetBoolean());
        Assert.Equal("sale-1", receipt.GetProperty("transactionKey").GetString());
        var request = Assert.Single(app.Gateway.Requests);
        Assert.Equal("/api/v2/transactions", request.Path);
        Assert.Equal("cc:sale", request.Body.GetProperty("command").GetString());
        Assert.Equal("single-use-token", request.Body.GetProperty("payment_key").GetString());
    }

    [Fact]
    public async Task DeclinedInitialChargeStopsBeforeSavingTheCard()
    {
        using var app = new PortalFactory();
        using var client = app.CreateClient();
        app.Gateway.Reply(Customer);
        app.Gateway.Reply(Decline);

        using var response = await client.PostAsJsonAsync("/api/payment/customers", SaveRequest);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var failure = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(failure.TryGetProperty("initialCharge", out _));
        Assert.Equal(new[] { "/api/v2/customers", "/api/v2/transactions" }, app.Gateway.Requests.Select(r => r.Path));
        var customers = await client.GetFromJsonAsync<JsonElement>("/api/payment/recurring");
        Assert.Empty(customers.EnumerateArray());
    }

    [Fact]
    public async Task SavedMethodUsesTheInitialSaleReferenceAndCanBeChargedAgain()
    {
        using var app = new PortalFactory();
        using var client = app.CreateClient();
        app.Gateway.Reply(Customer);
        app.Gateway.Reply(Approval);
        app.Gateway.Reply("""[{"key":"method-1"}]""");
        app.Gateway.Reply("""{"result_code":"A","key":"recurring-1"}""");

        using var saved = await client.PostAsJsonAsync("/api/payment/customers", SaveRequest);
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        var savedBody = await saved.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("sale-1", savedBody.GetProperty("initialCharge").GetProperty("transactionKey").GetString());
        using var run = await client.PostAsync("/api/payment/recurring/run", null);
        Assert.Equal(HttpStatusCode.OK, run.StatusCode);

        Assert.Equal(new[] { "/api/v2/customers", "/api/v2/transactions",
            "/api/v2/customers/customer-1/payment_methods", "/api/v2/transactions" },
            app.Gateway.Requests.Select(r => r.Path));
        var sale = app.Gateway.Requests[1].Body;
        Assert.Equal("single-use-token", sale.GetProperty("payment_key").GetString());
        Assert.Equal("customer-1", sale.GetProperty("custkey").GetString());
        var method = Assert.Single(app.Gateway.Requests[2].Body.EnumerateArray());
        Assert.Equal("transaction", method.GetProperty("pay_type").GetString());
        Assert.Equal("sale-1", method.GetProperty("transaction_key").GetString());
        var recurring = app.Gateway.Requests[3].Body;
        Assert.Equal("customer:sale", recurring.GetProperty("command").GetString());
        Assert.Equal("customer-1", recurring.GetProperty("custkey").GetString());
        Assert.Equal("method-1", recurring.GetProperty("paymethod_key").GetString());
        Assert.False(recurring.TryGetProperty("payment_key", out _));
        var customers = await client.GetFromJsonAsync<JsonElement[]>("/api/payment/recurring");
        var history = Assert.Single(Assert.Single(customers!).GetProperty("payments").EnumerateArray());
        Assert.Equal(1.00m, history.GetProperty("amount").GetDecimal());
        Assert.Equal("recurring-1", history.GetProperty("transactionKey").GetString());
    }

    [Theory]
    [InlineData(HttpStatusCode.UnprocessableEntity, """{"error":"vault rejected the request"}""")]
    [InlineData(HttpStatusCode.ServiceUnavailable, """[{"key":"unconfirmed-method"}]""")]
    [InlineData(HttpStatusCode.OK, """[{"error":"vault rejected the request"}]""")]
    [InlineData(HttpStatusCode.OK, """[{}]""")]
    [InlineData(HttpStatusCode.OK, "[]")]
    [InlineData(HttpStatusCode.OK, "invalid JSON")]
    [InlineData(HttpStatusCode.OK, null)] // Timeout before a response is available.
    public async Task VaultFailureAfterApprovalReturnsTheReceiptAndNeverEnrollsOrResubmits(
        HttpStatusCode vaultStatus, string? vaultResponse)
    {
        using var app = new PortalFactory();
        using var client = app.CreateClient();
        app.Gateway.Reply(Customer);
        app.Gateway.Reply(Approval);
        if (vaultResponse is null)
            app.Gateway.Timeout();
        else
            app.Gateway.Reply(vaultResponse, vaultStatus);

        using var response = await client.PostAsJsonAsync("/api/payment/customers", SaveRequest);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var failure = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(string.IsNullOrWhiteSpace(failure.GetProperty("error").GetString()));
        var receipt = failure.GetProperty("initialCharge");
        Assert.True(receipt.GetProperty("success").GetBoolean());
        Assert.Equal("sale-1", receipt.GetProperty("transactionKey").GetString());
        var customers = await client.GetFromJsonAsync<JsonElement>("/api/payment/recurring");
        Assert.Empty(customers.EnumerateArray());
        Assert.Equal(new[] { "/api/v2/customers", "/api/v2/transactions", "/api/v2/customers/customer-1/payment_methods" },
            app.Gateway.Requests.Select(r => r.Path));
    }

    [Fact]
    public async Task ApprovedSaleWithoutATransactionReferenceCannotBeVaulted()
    {
        using var app = new PortalFactory();
        using var client = app.CreateClient();
        app.Gateway.Reply(Customer);
        app.Gateway.Reply("""{"result_code":"A","authcode":"auth-1"}""");

        using var response = await client.PostAsJsonAsync("/api/payment/customers", SaveRequest);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var failure = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(failure.GetProperty("initialCharge").GetProperty("success").GetBoolean());
        Assert.Equal(new[] { "/api/v2/customers", "/api/v2/transactions" }, app.Gateway.Requests.Select(r => r.Path));
        var customers = await client.GetFromJsonAsync<JsonElement>("/api/payment/recurring");
        Assert.Empty(customers.EnumerateArray());
    }
}
