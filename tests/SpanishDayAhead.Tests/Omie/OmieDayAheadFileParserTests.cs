using System.Globalization;
using System.Text;
using SpanishDayAhead.Infrastructure.Omie;
using Xunit;

namespace SpanishDayAhead.Tests.Omie;

public sealed class OmieDayAheadFileParserTests
{
    private readonly OmieDayAheadFileParser _parser = new();

    [Fact]
    public async Task ParseAsync_ValidQuarterHourlyFile_Returns96Prices()
    {
        // Arrange
        var deliveryDate = new DateOnly(2026, 7, 11);

        var content = BuildFile(
            deliveryDate,
            periodCount: 96,
            spanishPriceFactory: period =>
                period == 1
                    ? 160.31m
                    : 100m + period);

        await using var stream = CreateStream(content);

        var importedAtUtc = new DateTimeOffset(
            2026,
            7,
            10,
            14,
            30,
            0,
            TimeSpan.Zero);

        // Act
        var results = await _parser.ParseAsync(
            stream,
            "marginalpdbc_20260711.1",
            importedAtUtc);

        // Assert
        Assert.Equal(96, results.Count);

        var first = results[0];

        Assert.Equal(deliveryDate, first.DeliveryDate);
        Assert.Equal(1, first.Period);
        Assert.Equal(15, first.ResolutionMinutes);
        Assert.Equal(160.31m, first.PriceEurPerMWh);
        Assert.Equal("ES", first.BiddingZone);

        Assert.Equal(
            "marginalpdbc_20260711.1",
            first.SourceFileName);

        Assert.Equal(1, first.SourceVersion);
        Assert.Equal(importedAtUtc, first.ImportedAtUtc);

        // 00:00 in Madrid during summer is 22:00 UTC
        // on the previous calendar day.
        Assert.Equal(
            new DateTimeOffset(
                2026,
                7,
                10,
                22,
                0,
                0,
                TimeSpan.Zero),
            first.DeliveryStartUtc);

        var last = results[^1];

        Assert.Equal(96, last.Period);

        Assert.Equal(
            new DateTimeOffset(
                2026,
                7,
                11,
                21,
                45,
                0,
                TimeSpan.Zero),
            last.DeliveryStartUtc);
    }

    [Fact]
    public async Task ParseAsync_SpringDstDay_Accepts92Periods()
    {
        // Spain has a 23-hour delivery day when clocks move forward.
        var deliveryDate = new DateOnly(2026, 3, 29);

        var content = BuildFile(
            deliveryDate,
            periodCount: 92);

        await using var stream = CreateStream(content);

        // Act
        var results = await _parser.ParseAsync(
            stream,
            "marginalpdbc_20260329.1",
            DateTimeOffset.UtcNow);

        // Assert
        Assert.Equal(92, results.Count);

        Assert.All(
            results,
            result => Assert.Equal(
                15,
                result.ResolutionMinutes));
    }

    [Fact]
    public async Task ParseAsync_AutumnDstDay_Accepts100Periods()
    {
        // Spain has a 25-hour delivery day when clocks move backward.
        var deliveryDate = new DateOnly(2026, 10, 25);

        var content = BuildFile(
            deliveryDate,
            periodCount: 100);

        await using var stream = CreateStream(content);

        // Act
        var results = await _parser.ParseAsync(
            stream,
            "marginalpdbc_20261025.2",
            DateTimeOffset.UtcNow);

        // Assert
        Assert.Equal(100, results.Count);

        Assert.All(
            results,
            result => Assert.Equal(
                15,
                result.ResolutionMinutes));

        Assert.All(
            results,
            result => Assert.Equal(
                2,
                result.SourceVersion));
    }

    [Fact]
    public async Task ParseAsync_NegativeSpanishPrice_IsAccepted()
    {
        // Arrange
        var deliveryDate = new DateOnly(2026, 7, 11);

        var content = BuildFile(
            deliveryDate,
            periodCount: 96,
            spanishPriceFactory: period =>
                period == 50
                    ? -0.01m
                    : 50m);

        await using var stream = CreateStream(content);

        // Act
        var results = await _parser.ParseAsync(
            stream,
            "marginalpdbc_20260711.1",
            DateTimeOffset.UtcNow);

        // Assert
        Assert.Equal(
            -0.01m,
            results[49].PriceEurPerMWh);
    }

    [Fact]
    public async Task ParseAsync_InvalidHeader_ThrowsFormatException()
    {
        // Arrange
        var content =
            "WRONGHEADER;\n" +
            "2026;07;11;1;10.00;20.00;\n" +
            "*\n";

        await using var stream = CreateStream(content);

        // Act and assert
        await Assert.ThrowsAsync<FormatException>(
            () => _parser.ParseAsync(
                stream,
                "marginalpdbc_20260711.1",
                DateTimeOffset.UtcNow));
    }

    [Fact]
    public async Task ParseAsync_MissingFinalMarker_ThrowsFormatException()
    {
        // Arrange
        var deliveryDate = new DateOnly(2026, 7, 11);

        var content = BuildFile(
            deliveryDate,
            periodCount: 96,
            includeFinalMarker: false);

        await using var stream = CreateStream(content);

        // Act and assert
        await Assert.ThrowsAsync<FormatException>(
            () => _parser.ParseAsync(
                stream,
                "marginalpdbc_20260711.1",
                DateTimeOffset.UtcNow));
    }

    [Fact]
    public async Task ParseAsync_DateDoesNotMatchFilename_ThrowsFormatException()
    {
        // Arrange
        var content = BuildFile(
            new DateOnly(2026, 7, 10),
            periodCount: 96);

        await using var stream = CreateStream(content);

        // Act and assert
        await Assert.ThrowsAsync<FormatException>(
            () => _parser.ParseAsync(
                stream,
                "marginalpdbc_20260711.1",
                DateTimeOffset.UtcNow));
    }

    [Fact]
    public async Task ParseAsync_MissingPeriod_ThrowsFormatException()
    {
        // Arrange
        var deliveryDate = new DateOnly(2026, 7, 11);
        var builder = new StringBuilder();

        builder.AppendLine("MARGINALPDBC;");

        for (var period = 1; period <= 96; period++)
        {
            // Period 25 is omitted and period 26 is written twice.
            var writtenPeriod =
                period == 25
                    ? 26
                    : period;

            builder.AppendLine(
                CreateRow(
                    deliveryDate,
                    writtenPeriod,
                    portuguesePrice: 50m,
                    spanishPrice: 60m));
        }

        builder.AppendLine("*");

        await using var stream =
            CreateStream(builder.ToString());

        // Act and assert
        await Assert.ThrowsAsync<FormatException>(
            () => _parser.ParseAsync(
                stream,
                "marginalpdbc_20260711.1",
                DateTimeOffset.UtcNow));
    }

    private static string BuildFile(
        DateOnly deliveryDate,
        int periodCount,
        Func<int, decimal>? spanishPriceFactory = null,
        bool includeFinalMarker = true)
    {
        spanishPriceFactory ??=
            period => 50m + period;

        var builder = new StringBuilder();

        builder.AppendLine("MARGINALPDBC;");

        for (var period = 1;
             period <= periodCount;
             period++)
        {
            builder.AppendLine(
                CreateRow(
                    deliveryDate,
                    period,
                    portuguesePrice: 40m + period,
                    spanishPrice:
                        spanishPriceFactory(period)));
        }

        if (includeFinalMarker)
        {
            builder.AppendLine("*");
        }

        return builder.ToString();
    }

    private static string CreateRow(
        DateOnly date,
        int period,
        decimal portuguesePrice,
        decimal spanishPrice)
    {
        return string.Format(
            CultureInfo.InvariantCulture,
            "{0};{1:00};{2:00};{3};{4:0.00};{5:0.00};",
            date.Year,
            date.Month,
            date.Day,
            period,
            portuguesePrice,
            spanishPrice);
    }

    private static MemoryStream CreateStream(
        string content)
    {
        return new MemoryStream(
            Encoding.UTF8.GetBytes(content));
    }
}