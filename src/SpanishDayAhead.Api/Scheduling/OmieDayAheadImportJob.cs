using Microsoft.Extensions.Options;
using Quartz;
using SpanishDayAhead.Application.Abstractions;

namespace SpanishDayAhead.Api.Scheduling;

/// <summary>
/// Periodically imports the latest available OMIE Day-Ahead
/// file revisions for the configured Spanish delivery date.
/// </summary>
[DisallowConcurrentExecution]
public sealed class OmieDayAheadImportJob : IJob
{
    private static readonly TimeZoneInfo SpanishMarketTimeZone =
        ResolveSpanishMarketTimeZone();

    private readonly IDayAheadImportService _importService;
    private readonly TimeProvider _timeProvider;
    private readonly IOptions<OmieImportScheduleOptions> _options;
    private readonly ILogger<OmieDayAheadImportJob> _logger;

    public OmieDayAheadImportJob(
        IDayAheadImportService importService,
        TimeProvider timeProvider,
        IOptions<OmieImportScheduleOptions> options,
        ILogger<OmieDayAheadImportJob> logger)
    {
        _importService = importService;
        _timeProvider = timeProvider;
        _options = options;
        _logger = logger;
    }

    public async Task Execute(
        IJobExecutionContext context)
    {
        var options = _options.Value;

        if (!options.Enabled)
        {
            _logger.LogInformation(
                "The scheduled OMIE import is disabled.");

            return;
        }

        var nowUtc = _timeProvider.GetUtcNow();

        var spanishNow = TimeZoneInfo.ConvertTime(
            nowUtc,
            SpanishMarketTimeZone);

        var deliveryDate = DateOnly
            .FromDateTime(spanishNow.DateTime)
            .AddDays(options.DeliveryDayOffset);

        _logger.LogInformation(
            "Starting scheduled OMIE import for delivery date " +
            "{DeliveryDate}. Maximum version: {MaxVersion}.",
            deliveryDate,
            options.MaxVersion);

        var foundAtLeastOneFile = false;

        for (var version = 1;
             version <= options.MaxVersion;
             version++)
        {
            context.CancellationToken
                .ThrowIfCancellationRequested();

            var result = await _importService.ImportAsync(
                deliveryDate,
                version,
                context.CancellationToken);

            if (!result.FileFound)
            {
                if (!foundAtLeastOneFile)
                {
                    _logger.LogInformation(
                        "OMIE file version 1 for {DeliveryDate} " +
                        "has not been published yet.",
                        deliveryDate);
                }
                else
                {
                    _logger.LogInformation(
                        "No OMIE revision {Version} was found for " +
                        "{DeliveryDate}. Revision checking has stopped.",
                        version,
                        deliveryDate);
                }

                break;
            }

            foundAtLeastOneFile = true;

            _logger.LogInformation(
                "Scheduled OMIE import completed for " +
                "{DeliveryDate}, version {Version}. " +
                "Parsed: {ParsedCount}; inserted: {InsertedCount}; " +
                "updated: {UpdatedCount}; ignored: {IgnoredCount}.",
                deliveryDate,
                version,
                result.ParsedCount,
                result.InsertedCount,
                result.UpdatedCount,
                result.IgnoredCount);
        }

        _logger.LogInformation(
            "Scheduled OMIE import cycle finished for " +
            "{DeliveryDate}.",
            deliveryDate);
    }

    private static TimeZoneInfo ResolveSpanishMarketTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(
                "Europe/Madrid");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById(
                "Romance Standard Time");
        }
    }
}