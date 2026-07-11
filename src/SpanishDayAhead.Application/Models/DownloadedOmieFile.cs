namespace SpanishDayAhead.Application.Models;

/// <summary>
/// Represents one OMIE file downloaded into memory.
/// OMIE price files are small, so keeping the content in memory
/// avoids response-stream lifetime problems.
/// </summary>
public sealed record DownloadedOmieFile(
    string FileName,
    byte[] Content)
{
    /// <summary>
    /// Creates a read-only stream for the OMIE parser.
    /// </summary>
    public Stream OpenRead()
    {
        return new MemoryStream(
            Content,
            writable: false);
    }
}