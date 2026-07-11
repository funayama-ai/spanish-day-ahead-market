using SpanishDayAhead.Domain;

namespace SpanishDayAhead.Application.Abstractions;

/// <summary>
/// Defines persistence operations for Spanish Day-Ahead prices.
/// The Application layer does not know which database technology
/// implements this interface.
/// </summary>
public interface IDayAheadPriceRepository
{
    /// <summary>
    /// Returns all stored prices for one OMIE delivery date.
    /// </summary>
    Task<IReadOnlyList<DayAheadPrice>> GetByDeliveryDateAsync(
        DateOnly deliveryDate,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns stored prices within an optional UTC time range.
    /// </summary>
    Task<IReadOnlyList<DayAheadPrice>> GetRangeAsync(
        DateTimeOffset? fromUtc,
        DateTimeOffset? toUtc,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds new price records to the persistence store.
    /// </summary>
    Task AddRangeAsync(
        IEnumerable<DayAheadPrice> prices,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists all pending changes.
    /// </summary>
    Task SaveChangesAsync(
        CancellationToken cancellationToken = default);
}