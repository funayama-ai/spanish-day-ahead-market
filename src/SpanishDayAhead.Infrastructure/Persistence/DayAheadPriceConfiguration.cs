using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SpanishDayAhead.Domain;

namespace SpanishDayAhead.Infrastructure.Persistence;

/// <summary>
/// Defines how DayAheadPrice is stored in SQLite.
/// </summary>
public sealed class DayAheadPriceConfiguration
    : IEntityTypeConfiguration<DayAheadPrice>
{
    public void Configure(EntityTypeBuilder<DayAheadPrice> builder)
    {
        builder.ToTable("DayAheadPrices");

        builder.HasKey(price => price.Id);

        // The domain entity creates its own Guid identifier.
        builder.Property(price => price.Id)
            .ValueGeneratedNever();

        builder.Property(price => price.BiddingZone)
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(price => price.DeliveryDate)
            .IsRequired();

        builder.Property(price => price.Period)
            .IsRequired();

        builder.Property(price => price.ResolutionMinutes)
            .IsRequired();

        /*
         * SQLite cannot natively compare or order DateTimeOffset values.
         * Store UTC ticks as an INTEGER so range queries, ordering and
         * uniqueness checks remain reliable.
         */
        builder.Property(price => price.DeliveryStartUtc)
            .HasConversion(
                value => value.UtcDateTime.Ticks,
                value => new DateTimeOffset(value, TimeSpan.Zero))
            .HasColumnType("INTEGER")
            .IsRequired();

        /*
         * SQLite does not natively support decimal comparison.
         * Store the value as REAL while keeping decimal in the domain model.
         */
        builder.Property(price => price.PriceEurPerMWh)
            .HasConversion<double>()
            .HasColumnType("REAL")
            .IsRequired();

        builder.Property(price => price.SourceFileName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(price => price.SourceVersion)
            .IsRequired();

        builder.Property(price => price.ImportedAtUtc)
            .HasConversion(
                value => value.UtcDateTime.Ticks,
                value => new DateTimeOffset(value, TimeSpan.Zero))
            .HasColumnType("INTEGER")
            .IsRequired();

        /*
         * Business uniqueness rule:
         * one price per bidding zone and delivery interval.
         */
        builder.HasIndex(price => new
        {
            price.BiddingZone,
            price.DeliveryStartUtc
        })
            .IsUnique()
            .HasDatabaseName(
                "UX_DayAheadPrices_BiddingZone_DeliveryStartUtc");

        // Supports efficient API range queries.
        builder.HasIndex(price => price.DeliveryStartUtc)
            .HasDatabaseName(
                "IX_DayAheadPrices_DeliveryStartUtc");

        // Supports daily import and revision checks.
        builder.HasIndex(price => price.DeliveryDate)
            .HasDatabaseName(
                "IX_DayAheadPrices_DeliveryDate");
    }
}