using CurrencyConverter.Server;
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
