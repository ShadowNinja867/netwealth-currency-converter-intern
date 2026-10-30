using CurrencyConverter.Server;

var builder = WebApplication.CreateBuilder(args);

// Stop waiting after ten seconds if the external provider is unavailable.
const int CurrencyRequestTimeoutSeconds = 10;
builder.Services.AddHttpClient<ICurrencyProvider, CurrencyProvider>(client =>
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
app.MapGet("/api/currencies", CurrencyEndpoints.GetCurrencies);
app.MapGet("/api/convert", CurrencyEndpoints.ConvertCurrency);

app.MapFallbackToFile("index.html");

app.Run();
