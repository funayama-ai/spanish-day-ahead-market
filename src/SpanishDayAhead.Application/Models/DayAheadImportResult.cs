namespace SpanishDayAhead.Application.Models;

/// <summary>
/// Summarises one attempted import of an OMIE Day-Ahead file.
/// </summary>
public sealed record DayAheadImportResult(
    DateOnly DeliveryDate,
    int RequestedVersion,
    string? SourceFileName,
    bool FileFound,
    int ParsedCount,
    int InsertedCount,
    int UpdatedCount,
    int IgnoredCount)
{
    /// <summary>
    /// Number of database records changed by the import.
    /// </summary>
    public int ChangedCount =>
        InsertedCount + UpdatedCount;
}