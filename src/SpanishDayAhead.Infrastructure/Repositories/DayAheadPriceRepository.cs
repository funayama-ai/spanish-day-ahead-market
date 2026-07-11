using Microsoft.EntityFrameworkCore;
using SpanishDayAhead.Application.Abstractions;
using SpanishDayAhead.Domain;
using SpanishDayAhead.Infrastructure.Persistence;

namespace SpanishDayAhead.Infrastructure.Repositories;

/// <summary>
/// EF Core and SQLite implementation of the Day-Ahead price repository.
/// </summary>
public sealed class DayAheadPriceRepository
    : IDayAheadPriceRepository
{
    private readonly SpanishDayAheadDbContext _dbContext;

    public DayAheadPriceRepository(
        SpanishDayAheadDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <summary>
    /// Returns all stored prices for one delivery date.
    ///
    /// Tracking remains enabled because the import process may apply
    /// a newer OMIE revision to an existing entity and save the change.
    /// </summary>
    public async Task<IReadOnlyList<DayAheadPrice>>
        GetByDeliveryDateAsync(
            DateOnly deliveryDate,
            CancellationToken cancellationToken = default)
    {
        return await _dbContext.DayAheadPrices
            .Where(price => price.DeliveryDate == deliveryDate)
            .OrderBy(price => price.DeliveryStartUtc)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Returns stored prices within an optional UTC range.
    ///
    /// Tracking is disabled because API queries only read the data.
    /// </summary>
    public async Task<IReadOnlyList<DayAheadPrice>>
        GetRangeAsync(
            DateTimeOffset? fromUtc,
            DateTimeOffset? toUtc,
            CancellationToken cancellationToken = default)
    {
        var query = _dbContext.DayAheadPrices
            .AsNoTracking()
            .AsQueryable();

        if (fromUtc.HasValue)
        {
            var normalizedFromUtc = fromUtc.Value.ToUniversalTime();

            query = query.Where(
                price => price.DeliveryStartUtc >= normalizedFromUtc);
        }

        if (toUtc.HasValue)
        {
            var normalizedToUtc = toUtc.Value.ToUniversalTime();

            query = query.Where(
                price => price.DeliveryStartUtc < normalizedToUtc);
        }

        return await query
            .OrderBy(price => price.DeliveryStartUtc)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Adds new price records to the current database session.
    /// </summary>
    public async Task AddRangeAsync(
        IEnumerable<DayAheadPrice> prices,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(prices);

        await _dbContext.DayAheadPrices.AddRangeAsync(
            prices,
            cancellationToken);
    }

    /// <summary>
    /// Commits all pending additions and revisions to SQLite.
    /// </summary>
    public Task SaveChangesAsync(
        CancellationToken cancellationToken = default)
    {
        return _dbContext.SaveChangesAsync(cancellationToken);
    }
}