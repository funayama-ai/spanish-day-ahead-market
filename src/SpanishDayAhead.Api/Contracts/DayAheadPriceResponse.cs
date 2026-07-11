namespace SpanishDayAhead.Api.Contracts;

/// <summary>
/// Public REST API representation of one Spanish Day-Ahead price.
/// </summary>
public sealed record DayAheadPriceResponse(
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