using System.Text.Json;

namespace CurrencyConverter.Server;

public class CurrencyProvider
{
    private HttpClient httpClient;

    public CurrencyProvider(HttpClient httpClient)
    {
        this.httpClient = httpClient;
    }

    public async Task<List<string>> GetCurrenciesAsync(CancellationToken cancellationToken)
    {
        JsonElement response = await this.httpClient.GetFromJsonAsync<JsonElement>("currencies", cancellationToken);
        List<string> currencies = new List<string>();

        if (response.ValueKind != JsonValueKind.Array)
        {
            throw new JsonException("The currency provider returned an unexpected response.");
        }

        foreach (JsonElement currency in response.EnumerateArray())
        {
            if (currency.ValueKind != JsonValueKind.Object ||
                !currency.TryGetProperty("iso_code", out JsonElement code) ||
                code.ValueKind != JsonValueKind.String)
            {
                throw new JsonException("The currency provider returned an invalid currency.");
            }

            string? currencyCode = code.GetString();
            if (!string.IsNullOrWhiteSpace(currencyCode))
            {
                currencies.Add(currencyCode);
            }
        }

        if (currencies.Count == 0)
        {
            throw new JsonException("The currency provider returned no currencies.");
        }

        currencies.Sort(StringComparer.Ordinal);
        return currencies;
    }
}
