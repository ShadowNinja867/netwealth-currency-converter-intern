var builder = WebApplication.CreateBuilder(args);
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
app.UseBlazorFrameworkFiles();
app.UseStaticFiles();
app.MapStaticAssets();

app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }));

app.MapFallbackToFile("index.html");

app.Run();
