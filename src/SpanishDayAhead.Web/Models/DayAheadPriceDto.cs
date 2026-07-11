namespace SpanishDayAhead.Web.Models;

/// <summary>
/// REST API response model used by the Blazor frontend.
/// </summary>
public sealed record DayAheadPriceDto(
    string BiddingZone,
    DateOnly DeliveryDate,
    int Period,
    int ResolutionMinutes,
    DateTimeOffset DeliveryStartLocal,
    DateTimeOffset DeliveryStartUtc,
    decimal PriceEurPerMWh,
    string SourceFileName,
    int SourceVersion,
    DateTimeOffset ImportedAtUtc);