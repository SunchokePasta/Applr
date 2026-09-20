namespace Applr.Services.Options;

/// <summary>
/// Per-upstream HTTP client timeouts, bound once at startup from the
/// "Timeouts" section of appsettings. One value per upstream rather than a
/// single shared timeout, because ArbeitNow (a third-party public API) and
/// Applr.RestApi (our own, same-network service) have very different
/// reasonable ceilings.
/// </summary>
public sealed class TimeoutOptions : AppOptionsBase
{
    public const string SectionName = "Timeouts";

    /// <summary>Timeout, in seconds, for calls to the ArbeitNow job board API.</summary>
    public int ArbeitNowSeconds { get; set; } = 30;

    /// <summary>Timeout, in seconds, for calls to Applr.RestApi (the Trackr REST API).</summary>
    public int TrackrSeconds { get; set; } = 30;

    /// <summary>Timeout, in seconds, for calls to the Gmail API.</summary>
    public int GmailSeconds { get; set; } = 30;
}
