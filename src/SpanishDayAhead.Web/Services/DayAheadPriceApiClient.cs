using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using SpanishDayAhead.Web.Models;

namespace SpanishDayAhead.Web.Services;

/// <summary>
/// Calls the SpanishDayAhead REST API from the Blazor frontend.
/// The frontend never accesses SQLite or the repository directly.
/// </summary>
public sealed class DayAheadPriceApiClient
{
    public const string HttpClientName =
        "SpanishDayAheadApi";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<DayAheadPriceApiClient> _logger;

    public DayAheadPriceApiClient(
        IHttpClientFactory httpClientFactory,
        ILogger<DayAheadPriceApiClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    /// <summary>
    /// Gets all stored Spanish Day-Ahead prices for one
    /// OMIE delivery date.
    ///
    /// Returns an empty list when the REST API returns HTTP 404.
    /// Other unsuccessful HTTP responses raise an exception.
    /// </summary>
    public async Task<IReadOnlyList<DayAheadPriceDto>>
        GetByDeliveryDateAsync(
            DateOnly deliveryDate,
            CancellationToken cancellationToken = default)
    {
        var client =
            _httpClientFactory.CreateClient(
                HttpClientName);

        var deliveryDateText =
            deliveryDate.ToString(
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture);

        var requestUri =
            "api/day-ahead-prices" +
            "?deliveryDate=" +
            Uri.EscapeDataString(deliveryDateText);

        _logger.LogInformation(
            "Requesting Day-Ahead prices for delivery date " +
            "{DeliveryDate}.",
            deliveryDate);

        using var response =
            await client.GetAsync(
                requestUri,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            _logger.LogInformation(
                "No Day-Ahead prices were found for " +
                "{DeliveryDate}.",
                deliveryDate);

            return Array.Empty<DayAheadPriceDto>();
        }

        response.EnsureSuccessStatusCode();

        var prices =
            await response.Content
                .ReadFromJsonAsync<List<DayAheadPriceDto>>(
                    cancellationToken: cancellationToken);

        if (prices is null)
        {
            throw new InvalidDataException(
                "The Day-Ahead REST API returned an empty " +
                "or invalid JSON response.");
        }

        var orderedPrices = prices
            .OrderBy(price => price.DeliveryStartUtc)
            .ToList();

        _logger.LogInformation(
            "Received {RecordCount} Day-Ahead prices for " +
            "{DeliveryDate}.",
            orderedPrices.Count,
            deliveryDate);

        return orderedPrices;
    }
}