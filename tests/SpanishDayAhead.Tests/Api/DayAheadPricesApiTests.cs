using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Xunit;

namespace SpanishDayAhead.Tests.Api;

public sealed class DayAheadPricesApiTests
    : IClassFixture<SpanishDayAheadApiFactory>
{
    private readonly SpanishDayAheadApiFactory _factory;

    public DayAheadPricesApiTests(
        SpanishDayAheadApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetJson_ExistingDate_Returns96Prices()
    {
        // Arrange
        await _factory.ResetAndSeedDatabaseAsync();

        using var client =
            _factory.CreateHttpsClient();

        using var request =
            new HttpRequestMessage(
                HttpMethod.Get,
                "/api/day-ahead-prices" +
                "?deliveryDate=2026-07-11");

        request.Headers.Accept.Add(
            new MediaTypeWithQualityHeaderValue(
                "application/json"));

        // Act
        using var response =
            await client.SendAsync(request);

        // Assert
        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);

        Assert.Equal(
            "application/json",
            response.Content.Headers
                .ContentType?
                .MediaType);

        var responseBody =
            await response.Content
                .ReadAsStringAsync();

        using var document =
            JsonDocument.Parse(
                responseBody);

        var prices =
            document.RootElement;

        Assert.Equal(
            JsonValueKind.Array,
            prices.ValueKind);

        Assert.Equal(
            96,
            prices.GetArrayLength());

        var first =
            prices[0];

        Assert.Equal(
            "ES",
            first.GetProperty(
                "biddingZone")
                .GetString());

        Assert.Equal(
            "2026-07-11",
            first.GetProperty(
                "deliveryDate")
                .GetString());

        Assert.Equal(
            1,
            first.GetProperty(
                "period")
                .GetInt32());

        Assert.Equal(
            15,
            first.GetProperty(
                "resolutionMinutes")
                .GetInt32());

        Assert.Equal(
            160.31m,
            first.GetProperty(
                "priceEurPerMWh")
                .GetDecimal());

        Assert.Equal(
            "marginalpdbc_20260711.1",
            first.GetProperty(
                "sourceFileName")
                .GetString());
    }

    [Fact]
    public async Task GetText_ExistingDate_ReturnsSemicolonData()
    {
        // Arrange
        await _factory.ResetAndSeedDatabaseAsync();

        using var client =
            _factory.CreateHttpsClient();

        using var request =
            new HttpRequestMessage(
                HttpMethod.Get,
                "/api/day-ahead-prices" +
                "?deliveryDate=2026-07-11");

        request.Headers.Accept.Add(
            new MediaTypeWithQualityHeaderValue(
                "text/plain"));

        // Act
        using var response =
            await client.SendAsync(request);

        // Assert
        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);

        Assert.Equal(
            "text/plain",
            response.Content.Headers
                .ContentType?
                .MediaType);

        var responseBody =
            await response.Content
                .ReadAsStringAsync();

        var lines =
            responseBody.Split(
                new[]
                {
                    "\r\n",
                    "\n"
                },
                StringSplitOptions
                    .RemoveEmptyEntries);

        // One header plus 96 data rows.
        Assert.Equal(
            97,
            lines.Length);

        Assert.Equal(
            "bidding_zone;delivery_date;period;" +
            "resolution_minutes;delivery_start_local;" +
            "delivery_start_utc;price_eur_per_mwh;" +
            "source_file_name;source_version;" +
            "imported_at_utc",
            lines[0]);

        Assert.StartsWith(
            "ES;2026-07-11;1;15;",
            lines[1],
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetJson_MissingDate_Returns404()
    {
        // Arrange
        await _factory.ResetAndSeedDatabaseAsync();

        using var client =
            _factory.CreateHttpsClient();

        using var request =
            new HttpRequestMessage(
                HttpMethod.Get,
                "/api/day-ahead-prices" +
                "?deliveryDate=2026-07-12");

        request.Headers.Accept.Add(
            new MediaTypeWithQualityHeaderValue(
                "application/json"));

        // Act
        using var response =
            await client.SendAsync(request);

        // Assert
        Assert.Equal(
            HttpStatusCode.NotFound,
            response.StatusCode);

        var responseBody =
            await response.Content
                .ReadAsStringAsync();

        using var document =
            JsonDocument.Parse(
                responseBody);

        var problem =
            document.RootElement;

        Assert.Equal(
            404,
            problem.GetProperty(
                "status")
                .GetInt32());

        Assert.Equal(
            "Day-Ahead prices were not found.",
            problem.GetProperty(
                "title")
                .GetString());

        Assert.Contains(
            "2026-07-12",
            problem.GetProperty(
                "detail")
                .GetString(),
            StringComparison.Ordinal);
    }
}