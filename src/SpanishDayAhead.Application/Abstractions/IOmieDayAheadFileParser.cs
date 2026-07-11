using SpanishDayAhead.Domain;

namespace SpanishDayAhead.Application.Abstractions;

/// <summary>
/// Parses an official OMIE MARGINALPDBC file into
/// Spanish Day-Ahead price entities.
/// </summary>
public interface IOmieDayAheadFileParser
{
    Task<IReadOnlyList<DayAheadPrice>> ParseAsync(
        Stream fileStream,
        string sourceFileName,
        DateTimeOffset importedAtUtc,
        CancellationToken cancellationToken = default);
}