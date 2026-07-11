using SpanishDayAhead.Api.Contracts;
using System.Globalization;
using System.Net.Mime;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using SpanishDayAhead.Application.Abstractions;

namespace SpanishDayAhead.Api.Controllers;

/// <summary>
/// Exposes stored Spanish Day-Ahead auction prices.
/// </summary>
[ApiController]
[Route("api/day-ahead-prices")]
public sealed class DayAheadPricesController : ControllerBase
{
    private static readonly TimeZoneInfo SpanishMarketTimeZone =
        ResolveSpanishMarketTimeZone();

    private readonly IDayAheadPriceRepository _repository;
    private readonly ILogger<DayAheadPricesController> _logger;

    public DayAheadPricesController(
        IDayAheadPriceRepository repository,
        ILogger<DayAheadPricesController> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    /// <summary>
    /// Returns the Spanish Day-Ahead prices for one delivery date.
    /// </summary>
    /// <param name="deliveryDate">
    /// OMIE delivery date in yyyy-MM-dd format.
    /// </param>
    /// <param name="cancellationToken">
    /// Cancels the database query when the HTTP request is cancelled.
    /// </param>
    [HttpGet]
    [Produces(
        MediaTypeNames.Application.Json,
        MediaTypeNames.Text.Plain)]
    [ProducesResponseType(
        typeof(IReadOnlyList<DayAheadPriceResponse>),
        StatusCodes.Status200OK)]
    [ProducesResponseType(
        typeof(ProblemDetails),
        StatusCodes.Status400BadRequest)]
    [ProducesResponseType(
        typeof(ProblemDetails),
        StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAsync(
        [FromQuery] string? deliveryDate,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(deliveryDate))
        {
            return BadRequest(
                CreateProblemDetails(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Delivery date is required.",
                    detail:
                        "Provide deliveryDate in yyyy-MM-dd format. " +
                        "Example: deliveryDate=2026-07-11"));
        }

        if (!DateOnly.TryParseExact(
                deliveryDate,
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var parsedDeliveryDate))
        {
            return BadRequest(
                CreateProblemDetails(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Invalid delivery date.",
                    detail:
                        $"'{deliveryDate}' is not a valid date. " +
                        "Use yyyy-MM-dd format."));
        }

        var storedPrices =
            await _repository.GetByDeliveryDateAsync(
                parsedDeliveryDate,
                cancellationToken);

        if (storedPrices.Count == 0)
        {
            _logger.LogInformation(
                "No Day-Ahead prices were found for delivery date " +
                "{DeliveryDate}.",
                parsedDeliveryDate);

            return NotFound(
                CreateProblemDetails(
                    statusCode: StatusCodes.Status404NotFound,
                    title: "Day-Ahead prices were not found.",
                    detail:
                        "No stored prices exist for delivery date " +
                        $"{parsedDeliveryDate:yyyy-MM-dd}."));
        }

        var response = storedPrices
            .OrderBy(price => price.DeliveryStartUtc)
            .Select(price =>
            {
                var deliveryStartLocal =
                    TimeZoneInfo.ConvertTime(
                        price.DeliveryStartUtc,
                        SpanishMarketTimeZone);

                return new DayAheadPriceResponse(
                    BiddingZone: price.BiddingZone,
                    DeliveryDate: price.DeliveryDate,
                    Period: price.Period,
                    ResolutionMinutes:
                        price.ResolutionMinutes,
                    DeliveryStartLocal:
                        deliveryStartLocal,
                    DeliveryStartUtc:
                        price.DeliveryStartUtc,
                    PriceEurPerMWh:
                        price.PriceEurPerMWh,
                    SourceFileName:
                        price.SourceFileName,
                    SourceVersion:
                        price.SourceVersion,
                    ImportedAtUtc:
                        price.ImportedAtUtc);
            })
            .ToList();

        _logger.LogInformation(
            "Returning {RecordCount} Day-Ahead prices for " +
            "{DeliveryDate}.",
            response.Count,
            parsedDeliveryDate);

        if (RequestAcceptsPlainText())
        {
            var textResponse =
                BuildSemicolonSeparatedText(response);

            return Content(
                textResponse,
                MediaTypeNames.Text.Plain,
                Encoding.UTF8);
        }

        return Ok(response);
    }

    private bool RequestAcceptsPlainText()
    {
        var acceptHeader =
            Request.Headers.Accept.ToString();

        if (string.IsNullOrWhiteSpace(acceptHeader))
        {
            return false;
        }

        var mediaTypes = acceptHeader.Split(
            ',',
            StringSplitOptions.RemoveEmptyEntries |
            StringSplitOptions.TrimEntries);

        foreach (var mediaTypeWithParameters in mediaTypes)
        {
            var mediaType = mediaTypeWithParameters
                .Split(
                    ';',
                    count: 2,
                    StringSplitOptions.TrimEntries)[0];

            if (string.Equals(
                    mediaType,
                    MediaTypeNames.Text.Plain,
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static string BuildSemicolonSeparatedText(
        IReadOnlyList<DayAheadPriceResponse> prices)
    {
        var builder = new StringBuilder();

        builder.AppendLine(
            "bidding_zone;" +
            "delivery_date;" +
            "period;" +
            "resolution_minutes;" +
            "delivery_start_local;" +
            "delivery_start_utc;" +
            "price_eur_per_mwh;" +
            "source_file_name;" +
            "source_version;" +
            "imported_at_utc");

        foreach (var price in prices)
        {
            builder.Append(
                EscapeField(price.BiddingZone));

            builder.Append(';');

            builder.Append(
                price.DeliveryDate.ToString(
                    "yyyy-MM-dd",
                    CultureInfo.InvariantCulture));

            builder.Append(';');

            builder.Append(
                price.Period.ToString(
                    CultureInfo.InvariantCulture));

            builder.Append(';');

            builder.Append(
                price.ResolutionMinutes.ToString(
                    CultureInfo.InvariantCulture));

            builder.Append(';');

            builder.Append(
                price.DeliveryStartLocal.ToString(
                    "yyyy-MM-dd'T'HH:mm:sszzz",
                    CultureInfo.InvariantCulture));

            builder.Append(';');

            builder.Append(
                price.DeliveryStartUtc
                    .ToUniversalTime()
                    .ToString(
                        "yyyy-MM-dd'T'HH:mm:ss'Z'",
                        CultureInfo.InvariantCulture));

            builder.Append(';');

            builder.Append(
                price.PriceEurPerMWh.ToString(
                    "0.#####",
                    CultureInfo.InvariantCulture));

            builder.Append(';');

            builder.Append(
                EscapeField(price.SourceFileName));

            builder.Append(';');

            builder.Append(
                price.SourceVersion.ToString(
                    CultureInfo.InvariantCulture));

            builder.Append(';');

            builder.Append(
                price.ImportedAtUtc
                    .ToUniversalTime()
                    .ToString(
                        "yyyy-MM-dd'T'HH:mm:ss'Z'",
                        CultureInfo.InvariantCulture));

            builder.AppendLine();
        }

        return builder.ToString();
    }

    private static string EscapeField(
        string value)
    {
        if (!value.Contains(';') &&
            !value.Contains('"') &&
            !value.Contains('\r') &&
            !value.Contains('\n'))
        {
            return value;
        }

        return "\"" +
               value.Replace(
                   "\"",
                   "\"\"",
                   StringComparison.Ordinal) +
               "\"";
    }

    private static ProblemDetails CreateProblemDetails(
        int statusCode,
        string title,
        string detail)
    {
        return new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = detail
        };
    }

    private static TimeZoneInfo ResolveSpanishMarketTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(
                "Europe/Madrid");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById(
                "Romance Standard Time");
        }
    }
}