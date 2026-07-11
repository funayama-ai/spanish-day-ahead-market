using System.Globalization;
using System.Net;
using Microsoft.Extensions.Logging;
using SpanishDayAhead.Application.Abstractions;
using SpanishDayAhead.Application.Models;

namespace SpanishDayAhead.Infrastructure.Omie;

/// <summary>
/// Downloads official OMIE MARGINALPDBC Day-Ahead price files.
/// </summary>
public sealed class OmieDayAheadFileDownloader
    : IOmieDayAheadFileDownloader
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<OmieDayAheadFileDownloader> _logger;

    public OmieDayAheadFileDownloader(
        HttpClient httpClient,
        ILogger<OmieDayAheadFileDownloader> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<DownloadedOmieFile?> DownloadAsync(
        DateOnly deliveryDate,
        int version = 1,
        CancellationToken cancellationToken = default)
    {
        if (version <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(version),
                version,
                "The OMIE file version must be greater than zero.");
        }

        var dateText = deliveryDate.ToString(
            "yyyyMMdd",
            CultureInfo.InvariantCulture);

        var fileName =
            $"marginalpdbc_{dateText}.{version}";

        var requestPath =
            "en/file-download" +
            $"?filename={Uri.EscapeDataString(fileName)}" +
            "&parents=marginalpdbc";

        _logger.LogInformation(
            "Downloading OMIE file {FileName} for delivery date " +
            "{DeliveryDate} and version {Version}.",
            fileName,
            deliveryDate,
            version);

        using var response = await _httpClient.GetAsync(
            requestPath,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            _logger.LogInformation(
                "OMIE file {FileName} was not found. " +
                "It may not have been published yet.",
                fileName);

            return null;
        }

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "OMIE download for {FileName} failed with HTTP " +
                "status code {StatusCode}.",
                fileName,
                (int)response.StatusCode);
        }

        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsByteArrayAsync(
            cancellationToken);

        if (content.Length == 0)
        {
            throw new InvalidDataException(
                $"OMIE returned an empty file for '{fileName}'.");
        }

        _logger.LogInformation(
            "Downloaded OMIE file {FileName}. Size: {ByteCount} bytes.",
            fileName,
            content.Length);

        return new DownloadedOmieFile(
            fileName,
            content);
    }
}