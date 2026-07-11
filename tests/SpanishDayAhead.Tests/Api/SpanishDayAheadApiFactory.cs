using System.Globalization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using SpanishDayAhead.Infrastructure.Persistence;

namespace SpanishDayAhead.Tests.Api;

public sealed class SpanishDayAheadApiFactory
    : WebApplicationFactory<Program>
{
    private readonly string _databasePath =
        Path.Combine(
            Path.GetTempPath(),
            $"spanish-day-ahead-tests-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(
        IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            // Prevent Quartz and other background jobs from
            // running during REST API integration tests.
            services.RemoveAll<IHostedService>();

            // Remove the production DbContext registration.
            services.RemoveAll<
                DbContextOptions<SpanishDayAheadDbContext>>();

            services.RemoveAll<
                SpanishDayAheadDbContext>();

            // Register an isolated temporary SQLite database.
            services.AddDbContext<
                SpanishDayAheadDbContext>(
                options =>
                {
                    options.UseSqlite(
                        $"Data Source={_databasePath}");
                });
        });
    }

    public HttpClient CreateHttpsClient()
    {
        return CreateClient(
            new WebApplicationFactoryClientOptions
            {
                BaseAddress =
                    new Uri("https://localhost"),

                AllowAutoRedirect = false
            });
    }

    public async Task ResetAndSeedDatabaseAsync()
    {
        using var scope =
            Services.CreateScope();

        var dbContext =
            scope.ServiceProvider
                .GetRequiredService<
                    SpanishDayAheadDbContext>();

        await dbContext.Database
            .EnsureDeletedAsync();

        await dbContext.Database
            .MigrateAsync();

        await SeedDayAheadPricesAsync(
            dbContext);
    }

    private static async Task SeedDayAheadPricesAsync(
        SpanishDayAheadDbContext dbContext)
    {
        var deliveryDate =
            new DateOnly(
                2026,
                7,
                11);

        var firstDeliveryStartUtc =
            new DateTimeOffset(
                2026,
                7,
                10,
                22,
                0,
                0,
                TimeSpan.Zero);

        var importedAtUtc =
            new DateTimeOffset(
                2026,
                7,
                10,
                18,
                31,
                18,
                TimeSpan.Zero);

        await using var transaction =
            await dbContext.Database
                .BeginTransactionAsync();

        for (var period = 1;
             period <= 96;
             period++)
        {
            // The entity Id is a Guid, so every test record
            // must receive a valid Guid value.
            var id =
                Guid.NewGuid();

            var deliveryStartUtc =
                firstDeliveryStartUtc.AddMinutes(
                    (period - 1) * 15);

            var price =
                period switch
                {
                    1 => 160.31m,
                    50 => -0.01m,
                    _ => 100m + period
                };

            var deliveryDateText =
                deliveryDate.ToString(
                    "yyyy-MM-dd",
                    CultureInfo.InvariantCulture);

            var deliveryStartUtcText =
                deliveryStartUtc.ToString(
                    "O",
                    CultureInfo.InvariantCulture);

            var importedAtUtcText =
                importedAtUtc.ToString(
                    "O",
                    CultureInfo.InvariantCulture);

            var priceText =
                price.ToString(
                    CultureInfo.InvariantCulture);

            await dbContext.Database
                .ExecuteSqlRawAsync(
                    """
                    INSERT INTO "DayAheadPrices"
                    (
                        "Id",
                        "BiddingZone",
                        "DeliveryDate",
                        "DeliveryStartUtc",
                        "ImportedAtUtc",
                        "Period",
                        "PriceEurPerMWh",
                        "ResolutionMinutes",
                        "SourceFileName",
                        "SourceVersion"
                    )
                    VALUES
                    (
                        {0},
                        {1},
                        {2},
                        {3},
                        {4},
                        {5},
                        {6},
                        {7},
                        {8},
                        {9}
                    );
                    """,
                    id,
                    "ES",
                    deliveryDateText,
                    deliveryStartUtcText,
                    importedAtUtcText,
                    period,
                    priceText,
                    15,
                    "marginalpdbc_20260711.1",
                    1);
        }

        await transaction.CommitAsync();
    }

    protected override void Dispose(
        bool disposing)
    {
        base.Dispose(disposing);

        if (!disposing)
        {
            return;
        }

        TryDeleteFile(
            _databasePath);

        TryDeleteFile(
            $"{_databasePath}-wal");

        TryDeleteFile(
            $"{_databasePath}-shm");
    }

    private static void TryDeleteFile(
        string filePath)
    {
        try
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
        catch (IOException)
        {
            // Temporary test-file cleanup must not cause
            // an otherwise successful test to fail.
        }
        catch (UnauthorizedAccessException)
        {
            // Ignore temporary cleanup failure.
        }
    }
}