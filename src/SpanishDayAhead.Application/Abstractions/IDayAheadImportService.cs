using SpanishDayAhead.Application.Models;

namespace SpanishDayAhead.Application.Abstractions;

/// <summary>
/// Coordinates downloading, parsing and storing one OMIE
/// Spanish Day-Ahead auction-result file.
/// </summary>
public interface IDayAheadImportService
{
    Task<DayAheadImportResult> ImportAsync(
        DateOnly deliveryDate,
        int version = 1,
        CancellationToken cancellationToken = default);
}