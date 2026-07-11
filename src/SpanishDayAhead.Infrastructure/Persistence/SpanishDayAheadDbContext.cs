using Microsoft.EntityFrameworkCore;
using SpanishDayAhead.Domain;

namespace SpanishDayAhead.Infrastructure.Persistence;

/// <summary>
/// Represents the application's SQLite database session.
/// </summary>
public sealed class SpanishDayAheadDbContext : DbContext
{
    public SpanishDayAheadDbContext(
        DbContextOptions<SpanishDayAheadDbContext> options)
        : base(options)
    {
    }

    /// <summary>
    /// Stored Spanish Day-Ahead auction prices.
    /// </summary>
    public DbSet<DayAheadPrice> DayAheadPrices =>
        Set<DayAheadPrice>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(SpanishDayAheadDbContext).Assembly);
    }
}