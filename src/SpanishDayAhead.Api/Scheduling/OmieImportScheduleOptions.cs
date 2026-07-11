using System.ComponentModel.DataAnnotations;

namespace SpanishDayAhead.Api.Scheduling;

/// <summary>
/// Configuration for the scheduled OMIE Day-Ahead import.
/// Values are loaded from appsettings.json.
/// </summary>
public sealed class OmieImportScheduleOptions
{
    public const string SectionName =
        "OmieImportSchedule";

    /// <summary>
    /// Enables or disables scheduled imports.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Number of minutes between import attempts.
    /// </summary>
    [Range(1, 1440)]
    public int IntervalMinutes { get; set; } = 15;

    /// <summary>
    /// Highest OMIE file revision that the job will check.
    /// </summary>
    [Range(1, 20)]
    public int MaxVersion { get; set; } = 5;

    /// <summary>
    /// Number of calendar days after today in Spain
    /// that should be imported. The normal value is 1,
    /// meaning tomorrow's Day-Ahead delivery date.
    /// </summary>
    [Range(0, 7)]
    public int DeliveryDayOffset { get; set; } = 1;
}