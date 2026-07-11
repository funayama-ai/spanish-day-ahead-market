namespace SpanishDayAhead.Domain;

/// <summary>
/// Represents the official Spanish Day-Ahead clearing price
/// for one electricity delivery interval.
/// </summary>
public sealed class DayAheadPrice
{
    // Required for Entity Framework Core materialisation.
    private DayAheadPrice()
    {
    }

    public DayAheadPrice(
        DateOnly deliveryDate,
        int period,
        int resolutionMinutes,
        DateTimeOffset deliveryStartUtc,
        decimal priceEurPerMWh,
        string sourceFileName,
        int sourceVersion,
        DateTimeOffset importedAtUtc)
    {
        if (period <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(period),
                period,
                "Period must be greater than zero.");
        }

        if (resolutionMinutes <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(resolutionMinutes),
                resolutionMinutes,
                "Resolution must be greater than zero.");
        }

        if (string.IsNullOrWhiteSpace(sourceFileName))
        {
            throw new ArgumentException(
                "Source filename is required.",
                nameof(sourceFileName));
        }

        if (sourceVersion <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(sourceVersion),
                sourceVersion,
                "Source version must be greater than zero.");
        }

        Id = Guid.NewGuid();
        DeliveryDate = deliveryDate;
        Period = period;
        ResolutionMinutes = resolutionMinutes;
        DeliveryStartUtc = deliveryStartUtc.ToUniversalTime();
        PriceEurPerMWh = priceEurPerMWh;
        SourceFileName = sourceFileName.Trim();
        SourceVersion = sourceVersion;
        ImportedAtUtc = importedAtUtc.ToUniversalTime();
    }

    /// <summary>
    /// Internal database identifier.
    /// </summary>
    public Guid Id { get; private set; }

    /// <summary>
    /// Spanish electricity bidding zone.
    /// </summary>
    public string BiddingZone { get; private set; } = "ES";

    /// <summary>
    /// Delivery date from the original OMIE file.
    /// </summary>
    public DateOnly DeliveryDate { get; private set; }

    /// <summary>
    /// OMIE delivery-period number.
    /// </summary>
    public int Period { get; private set; }

    /// <summary>
    /// Length of the delivery interval in minutes.
    /// </summary>
    public int ResolutionMinutes { get; private set; }

    /// <summary>
    /// Unambiguous start time of the delivery interval in UTC.
    /// </summary>
    public DateTimeOffset DeliveryStartUtc { get; private set; }

    /// <summary>
    /// Official Spanish Day-Ahead price in EUR/MWh.
    /// Zero and negative prices are valid.
    /// </summary>
    public decimal PriceEurPerMWh { get; private set; }

    /// <summary>
    /// Original OMIE filename.
    /// Example: marginalpdbc_20260711.1
    /// </summary>
    public string SourceFileName { get; private set; } = string.Empty;

    /// <summary>
    /// OMIE file revision number.
    /// </summary>
    public int SourceVersion { get; private set; }

    /// <summary>
    /// UTC time when the application imported the record.
    /// </summary>
    public DateTimeOffset ImportedAtUtc { get; private set; }

    /// <summary>
    /// Applies a corrected price from a newer OMIE file version.
    /// Returns true when the existing entity was updated.
    /// </summary>
    public bool ApplyRevision(
        decimal priceEurPerMWh,
        string sourceFileName,
        int sourceVersion,
        DateTimeOffset importedAtUtc)
    {
        if (string.IsNullOrWhiteSpace(sourceFileName))
        {
            throw new ArgumentException(
                "Source filename is required.",
                nameof(sourceFileName));
        }

        if (sourceVersion <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(sourceVersion),
                sourceVersion,
                "Source version must be greater than zero.");
        }

        // Ignore the same OMIE version or an older version.
        if (sourceVersion <= SourceVersion)
        {
            return false;
        }

        PriceEurPerMWh = priceEurPerMWh;
        SourceFileName = sourceFileName.Trim();
        SourceVersion = sourceVersion;
        ImportedAtUtc = importedAtUtc.ToUniversalTime();

        return true;
    }
}