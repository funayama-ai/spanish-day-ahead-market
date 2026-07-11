using SpanishDayAhead.Web.Components;
using SpanishDayAhead.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services for the Blazor Server application.
builder.Services
    .AddRazorComponents()
    .AddInteractiveServerComponents();

// Read the REST API base URL from appsettings.json.
var apiBaseUrl =
    builder.Configuration["Api:BaseUrl"]
    ?? throw new InvalidOperationException(
        "API base URL is not configured. " +
        "Add 'Api:BaseUrl' to appsettings.json.");

if (!Uri.TryCreate(
        apiBaseUrl,
        UriKind.Absolute,
        out var apiBaseAddress))
{
    throw new InvalidOperationException(
        $"The configured API base URL '{apiBaseUrl}' is invalid.");
}

// Register the named HttpClient used by DayAheadPriceApiClient.
builder.Services.AddHttpClient(
    DayAheadPriceApiClient.HttpClientName,
    client =>
    {
        client.BaseAddress = apiBaseAddress;
        client.Timeout = TimeSpan.FromSeconds(30);
    });

// Register the frontend REST API client.
builder.Services.AddScoped<DayAheadPriceApiClient>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler(
        "/Error",
        createScopeForErrors: true);

    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute(
    "/not-found",
    createScopeForStatusCodePages: true);

app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();