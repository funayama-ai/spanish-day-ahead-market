using Microsoft.Extensions.Logging.Abstractions;
using SpanishDayAhead.Application.Abstractions;
using SpanishDayAhead.Application.Models;
using SpanishDayAhead.Application.Services;
using SpanishDayAhead.Domain;
using Xunit;

namespace SpanishDayAhead.Tests.Services;

public sealed class DayAheadImportServiceTests
{
    private static readonly DateOnly DeliveryDate =
        new(2026, 7, 11);

    private static readonly DateTimeOffset FixedImportTimeUtc =
        new(
            2026,
            7,
            10,
            14,
            30,
            0,
            TimeSpan.Zero);

    [Fact]
    public async Task ImportAsync_FileNotFound_DoesNotParseOrSave()
    {
        // Arrange
        var downloader = new StubDownloader
        {
            Result = null
        };

        var parser = new StubParser();
        var repository = new StubRepository();

        var service = CreateService(
            downloader,
            parser,
            repository);

        // Act
        var result = await service.ImportAsync(
            DeliveryDate,
            version: 1);

        // Assert
        Assert.False(result.FileFound);
        Assert.Equal(0, result.ParsedCount);
        Assert.Equal(0, result.ChangedCount);

        Assert.Equal(1, downloader.CallCount);
        Assert.Equal(0, parser.CallCount);
        Assert.Equal(0, repository.GetCallCount);
        Assert.Equal(0, repository.SaveCallCount);
    }

    [Fact]
    public async Task ImportAsync_NewPrices_AddsAllAndSavesOnce()
    {
        // Arrange
        var downloader = new StubDownloader
        {
            Result = CreateDownloadedFile(
                version: 1)
        };

        var parser = new StubParser
        {
            Results =
            [
                CreatePrice(
                    period: 1,
                    price: 50m,
                    version: 1),

                CreatePrice(
                    period: 2,
                    price: 60m,
                    version: 1)
            ]
        };

        var repository = new StubRepository();

        var service = CreateService(
            downloader,
            parser,
            repository);

        // Act
        var result = await service.ImportAsync(
            DeliveryDate,
            version: 1);

        // Assert
        Assert.True(result.FileFound);
        Assert.Equal(2, result.ParsedCount);
        Assert.Equal(2, result.InsertedCount);
        Assert.Equal(0, result.UpdatedCount);
        Assert.Equal(0, result.IgnoredCount);
        Assert.Equal(2, result.ChangedCount);

        Assert.Equal(2, repository.AddedPrices.Count);
        Assert.Equal(1, repository.SaveCallCount);

        Assert.Equal(
            FixedImportTimeUtc,
            parser.ReceivedImportedAtUtc);
    }

    [Fact]
    public async Task ImportAsync_SameVersion_IgnoresRecordWithoutSaving()
    {
        // Arrange
        var existingPrice = CreatePrice(
            period: 1,
            price: 50m,
            version: 1);

        var parsedPrice = CreatePrice(
            period: 1,
            price: 99m,
            version: 1);

        var downloader = new StubDownloader
        {
            Result = CreateDownloadedFile(
                version: 1)
        };

        var parser = new StubParser
        {
            Results = [parsedPrice]
        };

        var repository = new StubRepository
        {
            ExistingPrices = [existingPrice]
        };

        var service = CreateService(
            downloader,
            parser,
            repository);

        // Act
        var result = await service.ImportAsync(
            DeliveryDate,
            version: 1);

        // Assert
        Assert.Equal(0, result.InsertedCount);
        Assert.Equal(0, result.UpdatedCount);
        Assert.Equal(1, result.IgnoredCount);

        Assert.Equal(
            50m,
            existingPrice.PriceEurPerMWh);

        Assert.Equal(
            1,
            existingPrice.SourceVersion);

        Assert.Empty(repository.AddedPrices);
        Assert.Equal(0, repository.SaveCallCount);
    }

    [Fact]
    public async Task ImportAsync_NewerVersion_UpdatesRecordAndSaves()
    {
        // Arrange
        var existingPrice = CreatePrice(
            period: 1,
            price: 50m,
            version: 1);

        var revisedPrice = CreatePrice(
            period: 1,
            price: 75.25m,
            version: 2);

        var downloader = new StubDownloader
        {
            Result = CreateDownloadedFile(
                version: 2)
        };

        var parser = new StubParser
        {
            Results = [revisedPrice]
        };

        var repository = new StubRepository
        {
            ExistingPrices = [existingPrice]
        };

        var service = CreateService(
            downloader,
            parser,
            repository);

        // Act
        var result = await service.ImportAsync(
            DeliveryDate,
            version: 2);

        // Assert
        Assert.Equal(0, result.InsertedCount);
        Assert.Equal(1, result.UpdatedCount);
        Assert.Equal(0, result.IgnoredCount);

        Assert.Equal(
            75.25m,
            existingPrice.PriceEurPerMWh);

        Assert.Equal(
            2,
            existingPrice.SourceVersion);

        Assert.Equal(
            "marginalpdbc_20260711.2",
            existingPrice.SourceFileName);

        Assert.Equal(1, repository.SaveCallCount);
    }

    [Fact]
    public async Task ImportAsync_MixedPrices_ReturnsCorrectCounts()
    {
        // Arrange
        var existingVersionOne = CreatePrice(
            period: 1,
            price: 50m,
            version: 1);

        var existingVersionTwo = CreatePrice(
            period: 2,
            price: 60m,
            version: 2);

        var downloader = new StubDownloader
        {
            Result = CreateDownloadedFile(
                version: 2)
        };

        var parser = new StubParser
        {
            Results =
            [
                // Newer version: update existing period 1.
                CreatePrice(
                    period: 1,
                    price: 55m,
                    version: 2),

                // Older version: ignore period 2.
                CreatePrice(
                    period: 2,
                    price: 99m,
                    version: 1),

                // New interval: insert period 3.
                CreatePrice(
                    period: 3,
                    price: 70m,
                    version: 2)
            ]
        };

        var repository = new StubRepository
        {
            ExistingPrices =
            [
                existingVersionOne,
                existingVersionTwo
            ]
        };

        var service = CreateService(
            downloader,
            parser,
            repository);

        // Act
        var result = await service.ImportAsync(
            DeliveryDate,
            version: 2);

        // Assert
        Assert.Equal(3, result.ParsedCount);
        Assert.Equal(1, result.InsertedCount);
        Assert.Equal(1, result.UpdatedCount);
        Assert.Equal(1, result.IgnoredCount);
        Assert.Equal(2, result.ChangedCount);

        Assert.Single(repository.AddedPrices);

        Assert.Equal(
            3,
            repository.AddedPrices[0].Period);

        Assert.Equal(
            55m,
            existingVersionOne.PriceEurPerMWh);

        Assert.Equal(
            60m,
            existingVersionTwo.PriceEurPerMWh);

        Assert.Equal(1, repository.SaveCallCount);
    }

    [Fact]
    public async Task ImportAsync_InvalidVersion_ThrowsBeforeDownload()
    {
        // Arrange
        var downloader = new StubDownloader();
        var parser = new StubParser();
        var repository = new StubRepository();

        var service = CreateService(
            downloader,
            parser,
            repository);

        // Act and assert
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => service.ImportAsync(
                DeliveryDate,
                version: 0));

        Assert.Equal(0, downloader.CallCount);
        Assert.Equal(0, parser.CallCount);
        Assert.Equal(0, repository.GetCallCount);
    }

    private static DayAheadImportService CreateService(
        StubDownloader downloader,
        StubParser parser,
        StubRepository repository)
    {
        return new DayAheadImportService(
            downloader,
            parser,
            repository,
            new FixedTimeProvider(
                FixedImportTimeUtc),
            NullLogger<DayAheadImportService>.Instance);
    }

    private static DownloadedOmieFile CreateDownloadedFile(
        int version)
    {
        return new DownloadedOmieFile(
            $"marginalpdbc_20260711.{version}",
            [1, 2, 3]);
    }

    private static DayAheadPrice CreatePrice(
        int period,
        decimal price,
        int version)
    {
        var deliveryStartUtc =
            new DateTimeOffset(
                2026,
                7,
                10,
                22,
                0,
                0,
                TimeSpan.Zero)
            .AddMinutes(
                (period - 1) * 15);

        return new DayAheadPrice(
            DeliveryDate,
            period,
            resolutionMinutes: 15,
            deliveryStartUtc,
            price,
            $"marginalpdbc_20260711.{version}",
            version,
            FixedImportTimeUtc);
    }

    private sealed class StubDownloader
        : IOmieDayAheadFileDownloader
    {
        public DownloadedOmieFile? Result { get; init; }

        public int CallCount { get; private set; }

        public Task<DownloadedOmieFile?> DownloadAsync(
            DateOnly deliveryDate,
            int version = 1,
            CancellationToken cancellationToken = default)
        {
            CallCount++;

            return Task.FromResult(Result);
        }
    }

    private sealed class StubParser
        : IOmieDayAheadFileParser
    {
        public IReadOnlyList<DayAheadPrice> Results { get; init; } =
            Array.Empty<DayAheadPrice>();

        public int CallCount { get; private set; }

        public DateTimeOffset? ReceivedImportedAtUtc
        {
            get;
            private set;
        }

        public Task<IReadOnlyList<DayAheadPrice>> ParseAsync(
            Stream fileStream,
            string sourceFileName,
            DateTimeOffset importedAtUtc,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            ReceivedImportedAtUtc = importedAtUtc;

            return Task.FromResult(Results);
        }
    }

    private sealed class StubRepository
        : IDayAheadPriceRepository
    {
        public IReadOnlyList<DayAheadPrice> ExistingPrices
        {
            get;
            init;
        } = Array.Empty<DayAheadPrice>();

        public List<DayAheadPrice> AddedPrices { get; } = [];

        public int GetCallCount { get; private set; }

        public int SaveCallCount { get; private set; }

        public Task<IReadOnlyList<DayAheadPrice>>
            GetByDeliveryDateAsync(
                DateOnly deliveryDate,
                CancellationToken cancellationToken = default)
        {
            GetCallCount++;

            return Task.FromResult(
                ExistingPrices);
        }

        public Task<IReadOnlyList<DayAheadPrice>>
            GetRangeAsync(
                DateTimeOffset? fromUtc,
                DateTimeOffset? toUtc,
                CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<DayAheadPrice>>(
                Array.Empty<DayAheadPrice>());
        }

        public Task AddRangeAsync(
            IEnumerable<DayAheadPrice> prices,
            CancellationToken cancellationToken = default)
        {
            AddedPrices.AddRange(prices);

            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(
            CancellationToken cancellationToken = default)
        {
            SaveCallCount++;

            return Task.CompletedTask;
        }
    }

    private sealed class FixedTimeProvider
        : TimeProvider
    {
        private readonly DateTimeOffset _utcNow;

        public FixedTimeProvider(
            DateTimeOffset utcNow)
        {
            _utcNow = utcNow;
        }

        public override DateTimeOffset GetUtcNow()
        {
            return _utcNow;
        }
    }
}