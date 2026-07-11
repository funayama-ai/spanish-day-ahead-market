using Microsoft.Extensions.Logging;
using SpanishDayAhead.Application.Abstractions;
using SpanishDayAhead.Application.Models;
using SpanishDayAhead.Domain;

namespace SpanishDayAhead.Application.Services;

/// <summary>
/// Coordinates downloading, parsing and storing one official
/// OMIE MARGINALPDBC Day-Ahead result file.
/// </summary>
public sealed class DayAheadImportService
    : IDayAheadImportService
{
    private readonly IOmieDayAheadFileDownloader _downloader;
    private readonly IOmieDayAheadFileParser _parser;
    private readonly IDayAheadPriceRepository _repository;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<DayAheadImportService> _logger;

    public DayAheadImportService(
        IOmieDayAheadFileDownloader downloader,
        IOmieDayAheadFileParser parser,
        IDayAheadPriceRepository repository,
        TimeProvider timeProvider,
        ILogger<DayAheadImportService> logger)
    {
        _downloader = downloader;
        _parser = parser;
        _repository = repository;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<DayAheadImportResult> ImportAsync(
        DateOnly deliveryDate,
        int version = 1,
        CancellationToken cancellationToken = default)
    {
        if (version <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(version),
                version,
                "The OMIE file version must be greater than zero.");
        }

        _logger.LogInformation(
            "Starting OMIE Day-Ahead import for delivery date " +
            "{DeliveryDate} and version {Version}.",
            deliveryDate,
            version);

        var downloadedFile =
            await _downloader.DownloadAsync(
                deliveryDate,
                version,
                cancellationToken);

        if (downloadedFile is null)
        {
            _logger.LogInformation(
                "No OMIE file was available for delivery date " +
                "{DeliveryDate} and version {Version}.",
                deliveryDate,
                version);

            return new DayAheadImportResult(
                DeliveryDate: deliveryDate,
                RequestedVersion: version,
                SourceFileName: null,
                FileFound: false,
                ParsedCount: 0,
                InsertedCount: 0,
                UpdatedCount: 0,
                IgnoredCount: 0);
        }

        var importedAtUtc =
            _timeProvider.GetUtcNow();

        await using var fileStream =
            downloadedFile.OpenRead();

        var parsedPrices =
            await _parser.ParseAsync(
                fileStream,
                downloadedFile.FileName,
                importedAtUtc,
                cancellationToken);

        // Additional safety check:
        // every parsed record must match the requested delivery date.
        if (parsedPrices.Any(
                price => price.DeliveryDate != deliveryDate))
        {
            throw new InvalidDataException(
                "The parsed OMIE file contains a delivery date " +
                "that does not match the requested delivery date.");
        }

        var existingPrices =
            await _repository.GetByDeliveryDateAsync(
                deliveryDate,
                cancellationToken);

        var existingByDeliveryStart =
            existingPrices.ToDictionary(
                price => price.DeliveryStartUtc);

        var pricesToInsert =
            new List<DayAheadPrice>();

        var insertedCount = 0;
        var updatedCount = 0;
        var ignoredCount = 0;

        foreach (var parsedPrice in parsedPrices)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!existingByDeliveryStart.TryGetValue(
                    parsedPrice.DeliveryStartUtc,
                    out var existingPrice))
            {
                pricesToInsert.Add(parsedPrice);

                existingByDeliveryStart.Add(
                    parsedPrice.DeliveryStartUtc,
                    parsedPrice);

                insertedCount++;
                continue;
            }

            var wasUpdated =
                existingPrice.ApplyRevision(
                    parsedPrice.PriceEurPerMWh,
                    parsedPrice.SourceFileName,
                    parsedPrice.SourceVersion,
                    parsedPrice.ImportedAtUtc);

            if (wasUpdated)
            {
                updatedCount++;
            }
            else
            {
                ignoredCount++;
            }
        }

        if (pricesToInsert.Count > 0)
        {
            await _repository.AddRangeAsync(
                pricesToInsert,
                cancellationToken);
        }

        // Save only when the import changed the database.
        if (insertedCount > 0 || updatedCount > 0)
        {
            await _repository.SaveChangesAsync(
                cancellationToken);
        }

        _logger.LogInformation(
            "Completed OMIE import for {DeliveryDate}. " +
            "File: {SourceFileName}; parsed: {ParsedCount}; " +
            "inserted: {InsertedCount}; updated: {UpdatedCount}; " +
            "ignored: {IgnoredCount}.",
            deliveryDate,
            downloadedFile.FileName,
            parsedPrices.Count,
            insertedCount,
            updatedCount,
            ignoredCount);

        return new DayAheadImportResult(
            DeliveryDate: deliveryDate,
            RequestedVersion: version,
            SourceFileName: downloadedFile.FileName,
            FileFound: true,
            ParsedCount: parsedPrices.Count,
            InsertedCount: insertedCount,
            UpdatedCount: updatedCount,
            IgnoredCount: ignoredCount);
    }
}