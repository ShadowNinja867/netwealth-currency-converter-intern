namespace CurrencyConverter.Client;

public class ConversionResponse
{
    // Success responses contain an amount; error responses contain a detail message.
    public decimal? ConvertedAmount { get; set; }
    public string? Detail { get; set; }
}
