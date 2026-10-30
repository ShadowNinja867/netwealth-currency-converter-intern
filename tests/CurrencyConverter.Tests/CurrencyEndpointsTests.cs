using CurrencyConverter.Server;
using Moq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using System.Globalization;
using System.Net;
using System.Text.Json;

namespace CurrencyConverter.Tests;

public class CurrencyEndpointsTests
{
    [Fact]
    public async Task ConvertsAmountUsingTheProviderRate()
    {
        Mock<ICurrencyProvider> provider = new Mock<ICurrencyProvider>(MockBehavior.Strict);
        provider.Setup(p => p.GetRateAsync("GBP", "EUR", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExchangeRate { Rate = 1.2m, Date = new DateOnly(2026, 1, 1) });

        IResult result = await CurrencyEndpoints.ConvertCurrency("GBP", "EUR", "100", provider.Object, CancellationToken.None);

        Assert.Equal(200, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
        JsonElement body = GetResponseBody(result);
        Assert.Equal(120m, body.GetProperty("convertedAmount").GetDecimal());
        Assert.Equal(1.2m, body.GetProperty("rate").GetDecimal());
        Assert.Equal(100m, body.GetProperty("amount").GetDecimal());
        Assert.Equal("GBP", body.GetProperty("from").GetString());
        Assert.Equal("EUR", body.GetProperty("to").GetString());
        Assert.Equal("2026-01-01", body.GetProperty("rateDate").GetString());
        provider.Verify(p => p.GetRateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once());
    }

    [Fact]
    public async Task PreservesDecimalPrecision()
    {
        Mock<ICurrencyProvider> provider = new Mock<ICurrencyProvider>(MockBehavior.Strict);
        provider.Setup(p => p.GetRateAsync("GBP", "EUR", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExchangeRate { Rate = 0.1m, Date = new DateOnly(2026, 1, 1) });

        IResult result = await CurrencyEndpoints.ConvertCurrency("GBP", "EUR", "0.2", provider.Object, CancellationToken.None);

        Assert.Equal(0.02m, GetResponseBody(result).GetProperty("convertedAmount").GetDecimal());
    }

    [Fact]
    public async Task TrimsAndUppercasesCurrencyCodes()
    {
        Mock<ICurrencyProvider> provider = new Mock<ICurrencyProvider>(MockBehavior.Strict);
        provider.Setup(p => p.GetRateAsync("GBP", "EUR", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExchangeRate { Rate = 1m, Date = new DateOnly(2026, 1, 1) });

        IResult result = await CurrencyEndpoints.ConvertCurrency(" gbp ", " eur ", "10", provider.Object, CancellationToken.None);

        Assert.Equal(200, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
        provider.Verify(p => p.GetRateAsync("GBP", "EUR", It.IsAny<CancellationToken>()), Times.Once());
    }

    [Fact]
    public async Task AcceptsZero()
    {
        Mock<ICurrencyProvider> provider = new Mock<ICurrencyProvider>(MockBehavior.Strict);
        provider.Setup(p => p.GetRateAsync("GBP", "EUR", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExchangeRate { Rate = 1.2m, Date = new DateOnly(2026, 1, 1) });

        IResult result = await CurrencyEndpoints.ConvertCurrency("GBP", "EUR", "0", provider.Object, CancellationToken.None);

        Assert.Equal(0m, GetResponseBody(result).GetProperty("convertedAmount").GetDecimal());
    }

    [Fact]
    public async Task ConvertsTheSameCurrencyUsingARateOfOne()
    {
        Mock<ICurrencyProvider> provider = new Mock<ICurrencyProvider>(MockBehavior.Strict);
        provider.Setup(p => p.GetRateAsync("GBP", "GBP", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExchangeRate { Rate = 1m, Date = new DateOnly(2026, 1, 1) });

        IResult result = await CurrencyEndpoints.ConvertCurrency("GBP", "GBP", "12.34", provider.Object, CancellationToken.None);

        Assert.Equal(12.34m, GetResponseBody(result).GetProperty("convertedAmount").GetDecimal());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("hello")]
    [InlineData("-1")]
    [InlineData("1e100")]
    public async Task RejectsInvalidAmountsWithoutCallingTheProvider(string? amount)
    {
        Mock<ICurrencyProvider> provider = new Mock<ICurrencyProvider>(MockBehavior.Strict);

        IResult result = await CurrencyEndpoints.ConvertCurrency("GBP", "EUR", amount, provider.Object, CancellationToken.None);

        ProblemHttpResult error = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(400, error.StatusCode);
        Assert.Equal("Enter a valid amount of money.", error.ProblemDetails.Detail);
        provider.Verify(p => p.GetRateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never());
    }

    [Theory]
    [InlineData(null, "EUR")]
    [InlineData("GBP", "")]
    [InlineData(" ", "EUR")]
    [InlineData("GB", "EUR")]
    public async Task RejectsMissingOrMalformedCurrencyCodes(string? from, string? to)
    {
        Mock<ICurrencyProvider> provider = new Mock<ICurrencyProvider>(MockBehavior.Strict);

        IResult result = await CurrencyEndpoints.ConvertCurrency(from, to, "100", provider.Object, CancellationToken.None);

        Assert.Equal(400, Assert.IsType<ProblemHttpResult>(result).StatusCode);
        provider.Verify(p => p.GetRateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never());
    }

    [Fact]
    public async Task ReturnsAnErrorIfTheCalculationOverflows()
    {
        Mock<ICurrencyProvider> provider = new Mock<ICurrencyProvider>(MockBehavior.Strict);
        provider.Setup(p => p.GetRateAsync("GBP", "EUR", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExchangeRate { Rate = 2m, Date = new DateOnly(2026, 1, 1) });
        string amount = decimal.MaxValue.ToString(CultureInfo.InvariantCulture);

        IResult result = await CurrencyEndpoints.ConvertCurrency("GBP", "EUR", amount, provider.Object, CancellationToken.None);

        ProblemHttpResult error = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(400, error.StatusCode);
        Assert.Contains("too large", error.ProblemDetails.Detail);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.UnprocessableEntity)]
    public async Task ReturnsAnErrorForUnsupportedCurrenciesOrMissingRates(HttpStatusCode providerStatus)
    {
        Mock<ICurrencyProvider> provider = new Mock<ICurrencyProvider>(MockBehavior.Strict);
        provider.Setup(p => p.GetRateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Provider rejected the pair.", null, providerStatus));

        IResult result = await CurrencyEndpoints.ConvertCurrency("ABC", "EUR", "100", provider.Object, CancellationToken.None);

        ProblemHttpResult error = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(400, error.StatusCode);
        Assert.Equal("No exchange rate is available for these currencies.", error.ProblemDetails.Detail);
    }

    [Fact]
    public async Task ReturnsAnErrorWhenTheProviderIsUnavailable()
    {
        Mock<ICurrencyProvider> provider = new Mock<ICurrencyProvider>(MockBehavior.Strict);
        provider.Setup(p => p.GetRateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Connection failed."));

        IResult result = await CurrencyEndpoints.ConvertCurrency("GBP", "EUR", "100", provider.Object, CancellationToken.None);

        Assert.Equal(503, Assert.IsType<ProblemHttpResult>(result).StatusCode);
    }

    [Fact]
    public async Task ReturnsAnErrorWhenTheProviderTimesOut()
    {
        Mock<ICurrencyProvider> provider = new Mock<ICurrencyProvider>(MockBehavior.Strict);
        provider.Setup(p => p.GetRateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TaskCanceledException("Provider timed out."));

        IResult result = await CurrencyEndpoints.ConvertCurrency("GBP", "EUR", "100", provider.Object, CancellationToken.None);

        Assert.Equal(504, Assert.IsType<ProblemHttpResult>(result).StatusCode);
    }

    [Fact]
    public async Task ReturnsAnErrorWhenTheProviderDataIsInvalid()
    {
        Mock<ICurrencyProvider> provider = new Mock<ICurrencyProvider>(MockBehavior.Strict);
        provider.Setup(p => p.GetRateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new JsonException("Invalid response."));

        IResult result = await CurrencyEndpoints.ConvertCurrency("GBP", "EUR", "100", provider.Object, CancellationToken.None);

        Assert.Equal(502, Assert.IsType<ProblemHttpResult>(result).StatusCode);
    }

    [Fact]
    public async Task DoesNotTreatACancelledRequestAsAProviderTimeout()
    {
        using CancellationTokenSource cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Mock<ICurrencyProvider> provider = new Mock<ICurrencyProvider>(MockBehavior.Strict);
        provider.Setup(p => p.GetRateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException(cancellation.Token));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            CurrencyEndpoints.ConvertCurrency("GBP", "EUR", "100", provider.Object, cancellation.Token));
    }

    [Fact]
    public async Task ReturnsTheCurrencyList()
    {
        Mock<ICurrencyProvider> provider = new Mock<ICurrencyProvider>(MockBehavior.Strict);
        provider.Setup(p => p.GetCurrenciesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string> { "EUR", "GBP", "USD" });

        IResult result = await CurrencyEndpoints.GetCurrencies(provider.Object, CancellationToken.None);

        Assert.Equal(200, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
        List<string> currencies = Assert.IsType<List<string>>(Assert.IsAssignableFrom<IValueHttpResult>(result).Value);
        Assert.Equal(new string[] { "EUR", "GBP", "USD" }, currencies);
    }

    [Fact]
    public async Task ReturnsAnErrorWhenFetchingCurrenciesFails()
    {
        Mock<ICurrencyProvider> provider = new Mock<ICurrencyProvider>(MockBehavior.Strict);
        provider.Setup(p => p.GetCurrenciesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Connection failed."));

        IResult result = await CurrencyEndpoints.GetCurrencies(provider.Object, CancellationToken.None);

        Assert.Equal(503, Assert.IsType<ProblemHttpResult>(result).StatusCode);
    }

    private JsonElement GetResponseBody(IResult result)
    {
        object? value = Assert.IsAssignableFrom<IValueHttpResult>(result).Value;
        return JsonSerializer.SerializeToElement(value);
    }
}
