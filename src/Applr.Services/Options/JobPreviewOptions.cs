namespace Applr.Services.Options;

/// <summary>
/// The job preview pane's browser environment, bound once at startup from the
/// "JobPreview" section of appsettings.
///
/// <see cref="RemoteDebuggingPort"/> in particular is not an implementation
/// detail and does not belong in a literal: ApplrFiller connects to that exact
/// port to drive the pane, so both sides have to name the same number
/// (APPLR_CDP_PORT on the Python side), and two copies of Applr on one machine
/// cannot both hold 9222 -- the second silently attaches to the first's
/// browser and fills forms in the wrong window.
/// </summary>
public sealed class JobPreviewOptions : AppOptionsBase
{
    public const string SectionName = "JobPreview";

    /// <summary>
    /// Port the preview pane's WebView2 exposes CDP on. Must match
    /// APPLR_CDP_PORT in ApplicationFiller/.env.
    /// </summary>
    public int RemoteDebuggingPort { get; set; } = 9222;

    /// <summary>Width, in pixels, the pane opens at before anyone drags it.</summary>
    public double WidthPixels { get; set; } = 540;

    /// <summary>
    /// Narrowest the pane can be dragged to. Not zero: a pane dragged to
    /// nothing takes the divider with it and there is then no way to drag it
    /// back -- closing it is what the close button is for.
    /// </summary>
    public double MinimumWidthPixels { get; set; } = 320;

    /// <summary>
    /// How long to wait for a job posting to finish loading before filling it.
    /// Apply navigates the pane and then hands the page to ApplrFiller, so
    /// this is the window in which the page has to become the page ApplrFiller
    /// will be told to work on.
    /// </summary>
    public int NavigationTimeoutSeconds { get; set; } = 45;

    public override void Validate()
    {
        if (RemoteDebuggingPort is < 1024 or > 65535)
        {
            throw new InvalidOperationException(
                $"{SectionName}:{nameof(RemoteDebuggingPort)} must be between 1024 and " +
                $"65535, not {RemoteDebuggingPort}.");
        }

        if (MinimumWidthPixels <= 0 || WidthPixels < MinimumWidthPixels)
        {
            throw new InvalidOperationException(
                $"{SectionName}:{nameof(WidthPixels)} ({WidthPixels}) must be at least " +
                $"{nameof(MinimumWidthPixels)} ({MinimumWidthPixels}), which must itself " +
                "be greater than zero.");
        }

        if (NavigationTimeoutSeconds <= 0)
        {
            throw new InvalidOperationException(
                $"{SectionName}:{nameof(NavigationTimeoutSeconds)} must be greater than " +
                $"zero, not {NavigationTimeoutSeconds}.");
        }
    }
}
