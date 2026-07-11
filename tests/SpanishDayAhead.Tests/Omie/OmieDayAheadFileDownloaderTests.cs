using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using SpanishDayAhead.Infrastructure.Omie;
using Xunit;

namespace SpanishDayAhead.Tests.Omie;

public sealed class OmieDayAheadFileDownloaderTests
{
    [Fact]
    public async Task DownloadAsync_SuccessfulResponse_ReturnsDownloadedFile()
    {
        // Arrange
        const string expectedContent =
            "MARGINALPDBC;\n" +
            "2026;07;11;1;50.00;60.00;\n" +
            "*\n";

        var handler = new StubHttpMessageHandler(
            request =>
            {
                Assert.Equal(
                    HttpMethod.Get,
                    request.Method);

                Assert.Equal(
                    "/en/file-download",
                    request.RequestUri?.AbsolutePath);

                Assert.Contains(
                    "filename=marginalpdbc_20260711.1",
                    request.RequestUri?.Query);

                Assert.Contains(
                    "parents=marginalpdbc",
                    request.RequestUri?.Query);

                return new HttpResponseMessage(
                    HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        expectedContent,
                        Encoding.UTF8)
                };
            });

        using var httpClient = CreateHttpClient(handler);

        var downloader = CreateDownloader(httpClient);

        // Act
        var result = await downloader.DownloadAsync(
            new DateOnly(2026, 7, 11),
            version: 1);

        // Assert
        Assert.NotNull(result);

        Assert.Equal(
            "marginalpdbc_20260711.1",
            result.FileName);

        Assert.Equal(
            expectedContent,
            Encoding.UTF8.GetString(result.Content));

        Assert.Equal(
            1,
            handler.RequestCount);
    }

    [Fact]
    public async Task DownloadAsync_NotFoundResponse_ReturnsNull()
    {
        // Arrange
        var handler = new StubHttpMessageHandler(
            _ => new HttpResponseMessage(
                HttpStatusCode.NotFound));

        using var httpClient = CreateHttpClient(handler);

        var downloader = CreateDownloader(httpClient);

        // Act
        var result = await downloader.DownloadAsync(
            new DateOnly(2026, 7, 11),
            version: 1);

        // Assert
        Assert.Null(result);
        Assert.Equal(1, handler.RequestCount);
    }

    [Fact]
    public async Task DownloadAsync_ServerError_ThrowsHttpRequestException()
    {
        // Arrange
        var handler = new StubHttpMessageHandler(
            _ => new HttpResponseMessage(
                HttpStatusCode.InternalServerError));

        using var httpClient = CreateHttpClient(handler);

        var downloader = CreateDownloader(httpClient);

        // Act and assert
        await Assert.ThrowsAsync<HttpRequestException>(
            () => downloader.DownloadAsync(
                new DateOnly(2026, 7, 11),
                version: 1));

        Assert.Equal(1, handler.RequestCount);
    }

    [Fact]
    public async Task DownloadAsync_EmptyResponse_ThrowsInvalidDataException()
    {
        // Arrange
        var handler = new StubHttpMessageHandler(
            _ => new HttpResponseMessage(
                HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(
                    Array.Empty<byte>())
            });

        using var httpClient = CreateHttpClient(handler);

        var downloader = CreateDownloader(httpClient);

        // Act and assert
        await Assert.ThrowsAsync<InvalidDataException>(
            () => downloader.DownloadAsync(
                new DateOnly(2026, 7, 11),
                version: 1));

        Assert.Equal(1, handler.RequestCount);
    }

    [Fact]
    public async Task DownloadAsync_InvalidVersion_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var handler = new StubHttpMessageHandler(
            _ => throw new InvalidOperationException(
                "No HTTP request should be sent."));

        using var httpClient = CreateHttpClient(handler);

        var downloader = CreateDownloader(httpClient);

        // Act and assert
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => downloader.DownloadAsync(
                new DateOnly(2026, 7, 11),
                version: 0));

        Assert.Equal(0, handler.RequestCount);
    }

    private static OmieDayAheadFileDownloader CreateDownloader(
        HttpClient httpClient)
    {
        return new OmieDayAheadFileDownloader(
            httpClient,
            NullLogger<OmieDayAheadFileDownloader>.Instance);
    }

    private static HttpClient CreateHttpClient(
        HttpMessageHandler handler)
    {
        return new HttpClient(handler)
        {
            BaseAddress = new Uri(
                "https://www.omie.es/")
        };
    }

    private sealed class StubHttpMessageHandler
        : HttpMessageHandler
    {
        private readonly Func<
            HttpRequestMessage,
            HttpResponseMessage> _responseFactory;

        public StubHttpMessageHandler(
            Func<
                HttpRequestMessage,
                HttpResponseMessage> responseFactory)
        {
            _responseFactory = responseFactory;
        }

        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestCount++;

            var response =
                _responseFactory(request);

            response.RequestMessage = request;

            return Task.FromResult(response);
        }
    }
}