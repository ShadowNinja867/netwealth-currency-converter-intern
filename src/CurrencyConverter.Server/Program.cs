using CurrencyConverter.Server;
using System.Globalization;
using System.Net;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

// Stop waiting after ten seconds if the external provider is unavailable.
const int CurrencyRequestTimeoutSeconds = 10;
builder.Services.AddHttpClient<CurrencyProvider>(client =>
{
    client.BaseAddress = new Uri("https://api.frankfurter.dev/v2/");
    client.Timeout = TimeSpan.FromSeconds(CurrencyRequestTimeoutSeconds);
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
}
else
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

// Serve the browser app from the same host as the API.
app.UseStaticFiles();
app.MapStaticAssets();

app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }));
app.MapGet("/api/currencies", GetCurrencies);
app.MapGet("/api/convert", ConvertCurrency);

app.MapFallbackToFile("index.html");

app.Run();

async Task<IResult> GetCurrencies(CurrencyProvider provider, CancellationToken cancellationToken)
{
    try
    {
        List<string> currencies = await provider.GetCurrenciesAsync(cancellationToken);
        return Results.Ok(currencies);
    }
    catch (HttpRequestException)
    {
        return Results.Problem("Currencies could not be loaded. Please try again.", statusCode: StatusCodes.Status503ServiceUnavailable);
    }
    catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
    {
        return Results.Problem("The currency provider took too long to respond. Please try again.", statusCode: StatusCodes.Status504GatewayTimeout);
    }
    catch (JsonException)
    {
        return Results.Problem("Currencies could not be loaded, Please try again.", statusCode: StatusCodes.Status502BadGateway);
    }
}

async Task<IResult> ConvertCurrency(string? from, string? to, string? amount, CurrencyProvider provider, CancellationToken cancellationToken)
{
    decimal amountValue;
    if (!decimal.TryParse(amount, NumberStyles.Float, CultureInfo.InvariantCulture, out amountValue) || amountValue < 0)
    {
        return Results.Problem("Enter a valid amount of money.", statusCode: StatusCodes.Status400BadRequest);
    }

    if (string.IsNullOrWhiteSpace(from) || string.IsNullOrWhiteSpace(to))
    {
        return Results.Problem("Select both currencies.", statusCode: StatusCodes.Status400BadRequest);
    }

    from = from.Trim().ToUpperInvariant();
    to = to.Trim().ToUpperInvariant();

    if (from.Length != 3 || to.Length != 3)
    {
        return Results.Problem("Enter three-character currency codes, such as GBP or EUR.", statusCode: StatusCodes.Status400BadRequest);
    }

    try
    {
        ExchangeRate exchangeRate = await provider.GetRateAsync(from, to, cancellationToken);
        decimal convertedAmount = amountValue * exchangeRate.Rate;

        return Results.Ok(new
        {
            from = from,
            to = to,
            amount = amountValue,
            rate = exchangeRate.Rate,
            convertedAmount = convertedAmount,
            rateDate = exchangeRate.Date
        });
    }
    catch (OverflowException)
    {
        return Results.Problem("The amount is too large to convert into the selected currency.", statusCode: StatusCodes.Status400BadRequest);
    }
    catch (HttpRequestException exception)
    {
        if (exception.StatusCode == HttpStatusCode.UnprocessableEntity ||
            exception.StatusCode == HttpStatusCode.BadRequest ||
            exception.StatusCode == HttpStatusCode.NotFound)
        {
            return Results.Problem("No exchange rate is available for these currencies.", statusCode: StatusCodes.Status400BadRequest);
        }
        return Results.Problem("The exchange-rate provider is unavailable. Please try again.", statusCode: StatusCodes.Status503ServiceUnavailable);
    }
    catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
    {
        return Results.Problem("The exchange-rate provider took too long to respond. Please try again.", statusCode: StatusCodes.Status504GatewayTimeout);
    }
    catch (JsonException)
    {
        return Results.Problem("The exchange-rate provider returned invalid data. Please try again.", statusCode: StatusCodes.Status502BadGateway);
    }
}
