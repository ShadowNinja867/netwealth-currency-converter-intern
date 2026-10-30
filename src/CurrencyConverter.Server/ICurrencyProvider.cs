namespace CurrencyConverter.Server;

public interface ICurrencyProvider
{
    Task<List<string>> GetCurrenciesAsync(CancellationToken cancellationToken);
    Task<ExchangeRate> GetRateAsync(string from, string to, CancellationToken cancellationToken);
}
