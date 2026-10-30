using Microsoft.AspNetCore.Http;
using System.Globalization;
using System.Net;
using System.Text.Json;

namespace CurrencyConverter.Server;

public static class CurrencyEndpoints
{
    public static async Task<IResult> GetCurrencies(ICurrencyProvider provider, CancellationToken cancellationToken)
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

    public static async Task<IResult> ConvertCurrency(string? from, string? to, string? amount, ICurrencyProvider provider, CancellationToken cancellationToken)
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
}
