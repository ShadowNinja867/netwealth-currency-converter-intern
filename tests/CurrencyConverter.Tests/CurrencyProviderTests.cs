using CurrencyConverter.Server;
using Moq;
using Moq.Protected;
using System.Net;
using System.Text;
using System.Text.Json;

namespace CurrencyConverter.Tests;

public class CurrencyProviderTests
{
    [Fact]
    public async Task ReadsTheRateAndDateFromTheResponse()
    {
        Mock<HttpMessageHandler> handler = CreateHandler(HttpStatusCode.OK,
            "{\"rate\":1.25,\"date\":\"2026-01-01\",\"base\":\"GBP\",\"quote\":\"EUR\"}");
        using HttpClient client = CreateClient(handler);
        CurrencyProvider provider = new CurrencyProvider(client);

        ExchangeRate rate = await provider.GetRateAsync("GBP", "EUR", CancellationToken.None);

        Assert.Equal(1.25m, rate.Rate);
        Assert.Equal(new DateOnly(2026, 1, 1), rate.Date);
        handler.Protected().Verify("SendAsync", Times.Once(),
            ItExpr.Is<HttpRequestMessage>(request => request.RequestUri != null && request.RequestUri.AbsoluteUri == "https://example.test/v2/rate/GBP/EUR"),
            ItExpr.IsAny<CancellationToken>());
    }

    [Theory]
    [InlineData("not JSON")]
    [InlineData("null")]
    [InlineData("{\"rate\":0,\"date\":\"2026-01-01\"}")]
    [InlineData("{\"rate\":1.25}")]
    public async Task RejectsInvalidRateData(string responseBody)
    {
        Mock<HttpMessageHandler> handler = CreateHandler(HttpStatusCode.OK, responseBody);
        using HttpClient client = CreateClient(handler);
        CurrencyProvider provider = new CurrencyProvider(client);

        await Assert.ThrowsAsync<JsonException>(() => provider.GetRateAsync("GBP", "EUR", CancellationToken.None));
    }

    [Theory]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.UnprocessableEntity)]
    public async Task PreservesTheProviderHttpError(HttpStatusCode statusCode)
    {
        Mock<HttpMessageHandler> handler = CreateHandler(statusCode, "{}");
        using HttpClient client = CreateClient(handler);
        CurrencyProvider provider = new CurrencyProvider(client);

        HttpRequestException error = await Assert.ThrowsAsync<HttpRequestException>(() =>
            provider.GetRateAsync("GBP", "EUR", CancellationToken.None));

        Assert.Equal(statusCode, error.StatusCode);
    }

    [Fact]
    public async Task ReturnsSortedCurrencyCodes()
    {
        Mock<HttpMessageHandler> handler = CreateHandler(HttpStatusCode.OK,
            "[{\"iso_code\":\"USD\"},{\"iso_code\":\"EUR\"},{\"iso_code\":\"GBP\"}]");
        using HttpClient client = CreateClient(handler);
        CurrencyProvider provider = new CurrencyProvider(client);

        List<string> currencies = await provider.GetCurrenciesAsync(CancellationToken.None);

        Assert.Equal(new string[] { "EUR", "GBP", "USD" }, currencies);
        handler.Protected().Verify("SendAsync", Times.Once(),
            ItExpr.Is<HttpRequestMessage>(request => request.RequestUri != null && request.RequestUri.AbsoluteUri == "https://example.test/v2/currencies"),
            ItExpr.IsAny<CancellationToken>());
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("{}")]
    [InlineData("[{\"iso_code\":123}]")]
    public async Task RejectsEmptyOrInvalidCurrencyLists(string responseBody)
    {
        Mock<HttpMessageHandler> handler = CreateHandler(HttpStatusCode.OK, responseBody);
        using HttpClient client = CreateClient(handler);
        CurrencyProvider provider = new CurrencyProvider(client);

        await Assert.ThrowsAsync<JsonException>(() => provider.GetCurrenciesAsync(CancellationToken.None));
    }

    [Fact]
    public async Task EscapesCurrencyCodesWhenBuildingTheRequestUrl()
    {
        Mock<HttpMessageHandler> handler = CreateHandler(HttpStatusCode.OK,
            "{\"rate\":1.25,\"date\":\"2026-01-01\"}");
        using HttpClient client = CreateClient(handler);
        CurrencyProvider provider = new CurrencyProvider(client);

        await provider.GetRateAsync("G/B", "EUR", CancellationToken.None);

        handler.Protected().Verify("SendAsync", Times.Once(),
            ItExpr.Is<HttpRequestMessage>(request => request.RequestUri != null && request.RequestUri.AbsoluteUri == "https://example.test/v2/rate/G%2FB/EUR"),
            ItExpr.IsAny<CancellationToken>());
    }

    private HttpClient CreateClient(Mock<HttpMessageHandler> handler)
    {
        HttpClient client = new HttpClient(handler.Object);
        client.BaseAddress = new Uri("https://example.test/v2/");
        return client;
    }

    private Mock<HttpMessageHandler> CreateHandler(HttpStatusCode statusCode, string responseBody)
    {
        Mock<HttpMessageHandler> handler = new Mock<HttpMessageHandler>();
        HttpResponseMessage response = new HttpResponseMessage(statusCode);
        response.Content = new StringContent(responseBody, Encoding.UTF8, "application/json");
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(response);
        return handler;
    }
}
