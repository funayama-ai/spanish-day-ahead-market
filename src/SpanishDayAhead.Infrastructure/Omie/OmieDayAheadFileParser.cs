using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using SpanishDayAhead.Application.Abstractions;
using SpanishDayAhead.Domain;

namespace SpanishDayAhead.Infrastructure.Omie;

/// <summary>
/// Parses official OMIE MARGINALPDBC Day-Ahead price files.
/// </summary>
public sealed partial class OmieDayAheadFileParser
    : IOmieDayAheadFileParser
{
    private static readonly TimeZoneInfo SpanishMarketTimeZone =
        ResolveSpanishMarketTimeZone();

    public async Task<IReadOnlyList<DayAheadPrice>> ParseAsync(
        Stream fileStream,
        string sourceFileName,
        DateTimeOffset importedAtUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(fileStream);

        if (!fileStream.CanRead)
        {
            throw new ArgumentException(
                "The OMIE file stream must be readable.",
                nameof(fileStream));
        }

        var sourceInformation =
            ParseSourceFileName(sourceFileName);

        using var reader = new StreamReader(
            fileStream,
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: true,
            bufferSize: 4096,
            leaveOpen: true);

        var firstLine =
            await reader.ReadLineAsync(cancellationToken);

        if (!string.Equals(
                firstLine?.Trim(),
                "MARGINALPDBC;",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new FormatException(
                "The file must begin with 'MARGINALPDBC;'.");
        }

        var rows = new List<RawOmiePriceRow>();
        var lineNumber = 1;
        var endMarkerFound = false;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var line =
                await reader.ReadLineAsync(cancellationToken);

            if (line is null)
            {
                break;
            }

            lineNumber++;

            var trimmedLine = line.Trim();

            if (trimmedLine.Length == 0)
            {
                continue;
            }

            if (trimmedLine == "*")
            {
                endMarkerFound = true;
                break;
            }

            rows.Add(
                ParseDataRow(
                    trimmedLine,
                    lineNumber,
                    sourceInformation.DeliveryDate));
        }

        if (!endMarkerFound)
        {
            throw new FormatException(
                "The file does not contain the final '*' marker.");
        }

        // Only blank lines are allowed after the final marker.
        while (true)
        {
            var remainingLine =
                await reader.ReadLineAsync(cancellationToken);

            if (remainingLine is null)
            {
                break;
            }

            if (!string.IsNullOrWhiteSpace(remainingLine))
            {
                throw new FormatException(
                    "Unexpected data was found after the '*' marker.");
            }
        }

        if (rows.Count == 0)
        {
            throw new FormatException(
                "The file contains no Day-Ahead price rows.");
        }

        ValidatePeriodSequence(rows);

        var localDayStartUtc =
            ConvertLocalMidnightToUtc(
                sourceInformation.DeliveryDate);

        var nextLocalDayStartUtc =
            ConvertLocalMidnightToUtc(
                sourceInformation.DeliveryDate.AddDays(1));

        var dayLengthMinutes = checked(
            (int)(nextLocalDayStartUtc - localDayStartUtc)
                .TotalMinutes);

        var resolutionMinutes =
            DetermineResolutionMinutes(
                rows.Count,
                dayLengthMinutes,
                sourceInformation.DeliveryDate);

        var prices = rows
            .Select(row =>
            {
                var deliveryStartUtc =
                    localDayStartUtc.AddMinutes(
                        (row.Period - 1) * resolutionMinutes);

                return new DayAheadPrice(
                    sourceInformation.DeliveryDate,
                    row.Period,
                    resolutionMinutes,
                    deliveryStartUtc,
                    row.SpanishPriceEurPerMWh,
                    sourceInformation.FileName,
                    sourceInformation.Version,
                    importedAtUtc);
            })
            .ToList();

        return prices;
    }

    private static RawOmiePriceRow ParseDataRow(
        string line,
        int lineNumber,
        DateOnly expectedDeliveryDate)
    {
        var fields = line.Split(';');

        if (fields.Length < 6)
        {
            throw new FormatException(
                $"Line {lineNumber} does not contain the six " +
                "required OMIE fields.");
        }

        // An official row normally has a trailing semicolon.
        // Additional non-empty fields are not accepted.
        if (fields
            .Skip(6)
            .Any(field => !string.IsNullOrWhiteSpace(field)))
        {
            throw new FormatException(
                $"Line {lineNumber} contains unexpected extra values.");
        }

        var year =
            ParseInteger(fields[0], "year", lineNumber);

        var month =
            ParseInteger(fields[1], "month", lineNumber);

        var day =
            ParseInteger(fields[2], "day", lineNumber);

        var period =
            ParseInteger(fields[3], "period", lineNumber);

        DateOnly deliveryDate;

        try
        {
            deliveryDate = new DateOnly(year, month, day);
        }
        catch (ArgumentOutOfRangeException exception)
        {
            throw new FormatException(
                $"Line {lineNumber} contains an invalid date.",
                exception);
        }

        if (deliveryDate != expectedDeliveryDate)
        {
            throw new FormatException(
                $"Line {lineNumber} contains delivery date " +
                $"{deliveryDate:yyyy-MM-dd}, but the filename " +
                $"identifies {expectedDeliveryDate:yyyy-MM-dd}.");
        }

        if (period <= 0)
        {
            throw new FormatException(
                $"Line {lineNumber} contains invalid period {period}.");
        }

        // Validate both published market-price fields.
        _ = ParsePrice(
            fields[4],
            "Portuguese price",
            lineNumber);

        var spanishPrice =
            ParsePrice(
                fields[5],
                "Spanish price",
                lineNumber);

        return new RawOmiePriceRow(
            period,
            spanishPrice);
    }

    private static decimal ParsePrice(
        string value,
        string fieldName,
        int lineNumber)
    {
        if (!decimal.TryParse(
                value.Trim(),
                NumberStyles.AllowLeadingSign |
                NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture,
                out var result))
        {
            throw new FormatException(
                $"Line {lineNumber} contains an invalid {fieldName}.");
        }

        return result;
    }

    private static int ParseInteger(
        string value,
        string fieldName,
        int lineNumber)
    {
        if (!int.TryParse(
                value.Trim(),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var result))
        {
            throw new FormatException(
                $"Line {lineNumber} contains an invalid {fieldName}.");
        }

        return result;
    }

    private static void ValidatePeriodSequence(
        IReadOnlyList<RawOmiePriceRow> rows)
    {
        for (var index = 0; index < rows.Count; index++)
        {
            var expectedPeriod = index + 1;
            var actualPeriod = rows[index].Period;

            if (actualPeriod != expectedPeriod)
            {
                throw new FormatException(
                    $"Expected period {expectedPeriod}, " +
                    $"but found period {actualPeriod}.");
            }
        }
    }

    private static int DetermineResolutionMinutes(
        int rowCount,
        int dayLengthMinutes,
        DateOnly deliveryDate)
    {
        var expectedQuarterHourlyCount =
            dayLengthMinutes / 15;

        var expectedHourlyCount =
            dayLengthMinutes / 60;

        if (rowCount == expectedQuarterHourlyCount)
        {
            return 15;
        }

        if (rowCount == expectedHourlyCount)
        {
            return 60;
        }

        throw new FormatException(
            $"The file contains {rowCount} periods for " +
            $"{deliveryDate:yyyy-MM-dd}. Expected " +
            $"{expectedQuarterHourlyCount} quarter-hourly periods " +
            $"or {expectedHourlyCount} hourly periods.");
    }

    private static SourceFileInformation ParseSourceFileName(
        string sourceFileName)
    {
        if (string.IsNullOrWhiteSpace(sourceFileName))
        {
            throw new ArgumentException(
                "The OMIE source filename is required.",
                nameof(sourceFileName));
        }

        var normalizedFileName =
            Path.GetFileName(sourceFileName.Trim());

        var match =
            SourceFileNamePattern().Match(normalizedFileName);

        if (!match.Success)
        {
            throw new FormatException(
                "The source filename must follow " +
                "'marginalpdbc_YYYYMMDD.v'.");
        }

        DateOnly deliveryDate;

        try
        {
            deliveryDate = DateOnly.ParseExact(
                match.Groups["date"].Value,
                "yyyyMMdd",
                CultureInfo.InvariantCulture);
        }
        catch (FormatException exception)
        {
            throw new FormatException(
                "The source filename contains an invalid date.",
                exception);
        }

        if (!int.TryParse(
                match.Groups["version"].Value,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var version) ||
            version <= 0)
        {
            throw new FormatException(
                "The source filename contains an invalid version.");
        }

        return new SourceFileInformation(
            normalizedFileName,
            deliveryDate,
            version);
    }

    private static DateTimeOffset ConvertLocalMidnightToUtc(
        DateOnly date)
    {
        var localMidnight = date.ToDateTime(
            TimeOnly.MinValue,
            DateTimeKind.Unspecified);

        var utcDateTime = TimeZoneInfo.ConvertTimeToUtc(
            localMidnight,
            SpanishMarketTimeZone);

        return new DateTimeOffset(
            utcDateTime,
            TimeSpan.Zero);
    }

    private static TimeZoneInfo ResolveSpanishMarketTimeZone()
    {
        try
        {
            // IANA ID used on Linux and supported by current .NET
            // installations on many Windows systems.
            return TimeZoneInfo.FindSystemTimeZoneById(
                "Europe/Madrid");
        }
        catch (TimeZoneNotFoundException)
        {
            // Windows fallback.
            return TimeZoneInfo.FindSystemTimeZoneById(
                "Romance Standard Time");
        }
    }

    [GeneratedRegex(
        @"^marginalpdbc_(?<date>\d{8})\.(?<version>\d+)(?:\.txt)?$",
        RegexOptions.IgnoreCase |
        RegexOptions.CultureInvariant)]
    private static partial Regex SourceFileNamePattern();

    private sealed record RawOmiePriceRow(
        int Period,
        decimal SpanishPriceEurPerMWh);

    private sealed record SourceFileInformation(
        string FileName,
        DateOnly DeliveryDate,
        int Version);
}