using SpanishDayAhead.Application.Models;

namespace SpanishDayAhead.Application.Abstractions;

/// <summary>
/// Downloads official OMIE MARGINALPDBC files.
/// </summary>
public interface IOmieDayAheadFileDownloader
{
    /// <summary>
    /// Downloads one OMIE file for an exact delivery date and version.
    ///
    /// Returns null when OMIE responds with HTTP 404, which normally
    /// means that the requested file has not been published.
    /// </summary>
    Task<DownloadedOmieFile?> DownloadAsync(
        DateOnly deliveryDate,
        int version = 1,
        CancellationToken cancellationToken = default);
}