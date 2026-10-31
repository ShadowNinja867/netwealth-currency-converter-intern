# Netwealth Currency Converter

A currency converter built with C#, ASP.NET Core, and Blazor WebAssembly for the Netwealth intern coding challenge.

[Use the app](https://abhinav-netwealth-currency-cdbabdh7gvene6bb.francecentral-01.azurewebsites.net/)

Select the source and destination currencies, enter an amount, and click **Convert**. The arrow button swaps the currencies.

Exchange rates are provided by Frankfurter. The app is hosted on Azure App Service's Free F1 plan and may take a little longer to load after inactivity.

## Run locally

Install the .NET 10 SDK, then run from the repository root:

```sh
dotnet run --project src/CurrencyConverter.Server --launch-profile http
```

Open http://localhost:5234.

## Run tests

```sh
dotnet test CurrencyConverter.slnx
```

The tests use xUnit and Moq and run without making external API calls.
