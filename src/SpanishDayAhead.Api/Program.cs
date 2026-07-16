using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Quartz;
using SpanishDayAhead.Api.Scheduling;
using SpanishDayAhead.Application.Abstractions;
using SpanishDayAhead.Application.Services;
using SpanishDayAhead.Infrastructure.Omie;
using SpanishDayAhead.Infrastructure.Persistence;
using SpanishDayAhead.Infrastructure.Repositories;

var builder = WebApplication.CreateBuilder(args);

// Register REST API controllers.
builder.Services.AddControllers();

// Register authorization services.
builder.Services.AddAuthorization();

// Register OpenAPI document generation.
builder.Services.AddOpenApi();

// ---------------------------------------------------------
// SQLite configuration
// ---------------------------------------------------------

var configuredConnectionString =
    builder.Configuration.GetConnectionString(
        "SpanishDayAhead")
    ?? throw new InvalidOperationException(
        "Connection string 'SpanishDayAhead' is not configured.");

var connectionStringBuilder =
    new SqliteConnectionStringBuilder(
        configuredConnectionString);

if (string.IsNullOrWhiteSpace(
        connectionStringBuilder.DataSource))
{
    throw new InvalidOperationException(
        "The SQLite connection string does not contain a data source.");
}

if (!Path.IsPathRooted(
        connectionStringBuilder.DataSource))
{
    var absoluteDatabasePath =
        Path.GetFullPath(
            Path.Combine(
                builder.Environment.ContentRootPath,
                connectionStringBuilder.DataSource));

    var databaseDirectory =
        Path.GetDirectoryName(
            absoluteDatabasePath);

    if (!string.IsNullOrWhiteSpace(
            databaseDirectory))
    {
        Directory.CreateDirectory(
            databaseDirectory);
    }

    connectionStringBuilder.DataSource =
        absoluteDatabasePath;
}

// Register the EF Core database context.
builder.Services.AddDbContext<
    SpanishDayAheadDbContext>(
    options =>
    {
        options.UseSqlite(
            connectionStringBuilder.ToString());
    });

// ---------------------------------------------------------
// Application and Infrastructure services
// ---------------------------------------------------------

// Register the repository implementation.
builder.Services.AddScoped<
    IDayAheadPriceRepository,
    DayAheadPriceRepository>();

// Register the system clock.
builder.Services.AddSingleton<TimeProvider>(
    TimeProvider.System);

// Register the complete Day-Ahead import workflow.
builder.Services.AddScoped<
    IDayAheadImportService,
    DayAheadImportService>();

// Register the stateless OMIE file parser.
builder.Services.AddSingleton<
    IOmieDayAheadFileParser,
    OmieDayAheadFileParser>();

// Register the OMIE downloader as a typed HttpClient.
builder.Services.AddHttpClient<
    IOmieDayAheadFileDownloader,
    OmieDayAheadFileDownloader>(
    (serviceProvider, client) =>
    {
        var configuration =
            serviceProvider.GetRequiredService<
                IConfiguration>();

        var baseUrl =
            configuration["Omie:BaseUrl"]
            ?? throw new InvalidOperationException(
                "OMIE base URL is not configured.");

        if (!Uri.TryCreate(
                baseUrl,
                UriKind.Absolute,
                out var baseAddress))
        {
            throw new InvalidOperationException(
                $"The configured OMIE base URL " +
                $"'{baseUrl}' is invalid.");
        }

        var timeoutSeconds =
            configuration.GetValue<int?>(
                "Omie:TimeoutSeconds")
            ?? 30;

        if (timeoutSeconds <= 0)
        {
            throw new InvalidOperationException(
                "OMIE timeout must be greater than zero.");
        }

        client.BaseAddress =
            baseAddress;

        client.Timeout =
            TimeSpan.FromSeconds(
                timeoutSeconds);

        client.DefaultRequestHeaders
            .UserAgent
            .ParseAdd(
                "SpanishDayAhead/1.0");
    });

// ---------------------------------------------------------
// Quartz schedule configuration
// ---------------------------------------------------------

var scheduleSection =
    builder.Configuration.GetSection(
        OmieImportScheduleOptions.SectionName);

var scheduleOptions =
    scheduleSection
        .Get<OmieImportScheduleOptions>()
    ?? new OmieImportScheduleOptions();

ValidateScheduleOptions(
    scheduleOptions);

// Register the configuration so that the Quartz job can
// receive IOptions<OmieImportScheduleOptions>.
builder.Services.Configure<
    OmieImportScheduleOptions>(
    scheduleSection);

var importJobKey =
    new JobKey(
        "OmieDayAheadImportJob",
        "Omie");

builder.Services.AddQuartz(
    quartz =>
    {
        quartz.AddJob<
            OmieDayAheadImportJob>(
            importJobKey,
            job => job
                .WithDescription(
                    "Imports official OMIE " +
                    "Spanish Day-Ahead prices."));

        quartz.AddTrigger(
            trigger => trigger
                .WithIdentity(
                    "OmieDayAheadImportTrigger",
                    "Omie")
                .ForJob(
                    importJobKey)
                .StartNow()
                .WithSimpleSchedule(
                    schedule => schedule
                        .WithInterval(
                            TimeSpan.FromMinutes(
                                scheduleOptions
                                    .IntervalMinutes))
                        .RepeatForever())
                .WithDescription(
                    "Runs the OMIE import immediately " +
                    "at startup and then at the " +
                    "configured interval."));
    });

// Start and stop Quartz together with the API application.
builder.Services.AddQuartzHostedService(
    options =>
    {
        // Allow an active import to complete during
        // graceful application shutdown.
        options.WaitForJobsToComplete =
            true;
    });

// ---------------------------------------------------------
// Build and configure the HTTP application
// ---------------------------------------------------------

var app =
    builder.Build();

// ---------------------------------------------------------
// Apply EF Core migrations before Quartz and the HTTP
// application begin using the database.
// ---------------------------------------------------------

await using (var scope =
    app.Services.CreateAsyncScope())
{
    var serviceProvider =
        scope.ServiceProvider;

    var logger =
        serviceProvider
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger(
                "DatabaseMigration");

    try
    {
        logger.LogInformation(
            "Applying pending database migrations.");

        var dbContext =
            serviceProvider
                .GetRequiredService<
                    SpanishDayAheadDbContext>();

        await dbContext.Database
            .MigrateAsync();

        logger.LogInformation(
            "Database migrations applied successfully.");
    }
    catch (Exception exception)
    {
        logger.LogCritical(
            exception,
            "Database migration failed during application startup.");

        throw;
    }
}

// Enable OpenAPI only in the development environment.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();

// ---------------------------------------------------------
// Local configuration validation
// ---------------------------------------------------------

static void ValidateScheduleOptions(
    OmieImportScheduleOptions options)
{
    if (options.IntervalMinutes is < 1 or > 1440)
    {
        throw new InvalidOperationException(
            "OmieImportSchedule:IntervalMinutes must be " +
            "between 1 and 1440.");
    }

    if (options.MaxVersion is < 1 or > 20)
    {
        throw new InvalidOperationException(
            "OmieImportSchedule:MaxVersion must be " +
            "between 1 and 20.");
    }

    if (options.DeliveryDayOffset is < 0 or > 7)
    {
        throw new InvalidOperationException(
            "OmieImportSchedule:DeliveryDayOffset must be " +
            "between 0 and 7.");
    }
}

// Expose the top-level API entry point to integration tests.
// This must remain after all top-level statements and
// local functions in this file.
public partial class Program
{
}