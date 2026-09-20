using System.Text.Json.Serialization;

namespace Applr.DesktopServices.Interfaces;

/// <summary>
/// Talks to ApplrFiller, the local Python service that drives the browser
/// over CDP and fills an application form from the stored profile.
///
/// Separate from <see cref="IApplrClient"/> because it is a different
/// service with a different base address, not another Applr.API route --
/// ApplrFiller is a peer of the desktop app, the same way TrackrScraper is
/// a peer of Applr.RestApi. Sharing a client would mean one BaseAddress
/// for two unrelated hosts.
///
/// Throws ApplrApiException and nothing else, same contract as IApplrClient:
/// the message is already phrased for a user and the reference already
/// appears in a log file, so callers never inspect a status code.
/// </summary>
public interface IApplrFillerClient
{
    /// <summary>
    /// Fills the application form for a job.
    ///
    /// <paramref name="url"/> is the page the desktop app has just finished
    /// opening in the preview pane -- the url the browser settled on, after
    /// any redirects, not the one stored against the job. ApplrFiller matches
    /// an open page against it rather than taking whichever page is newest,
    /// so a popup the site raised cannot end up receiving the user's details.
    /// Null means "whatever the browser has open", which is the behaviour the
    /// endpoint had before it took a url.
    ///
    /// jobId reaches ApplrFiller's log only, so one click can be followed
    /// across both logs; the fill itself does not use it.
    /// </summary>
    Task<ApplyResult> ApplyAsync(
        uint jobId,
        string? url = null,
        CancellationToken cancellationToken = default);
}

/// <summary>What ApplrFiller filled, and what it could not.</summary>
public sealed class ApplyResult
{
    public List<string> Filled { get; set; } = [];

    // ApplrFiller answers in snake_case. The Web serializer defaults are
    // camelCase and case-insensitive, which matches neither "not_filled" nor
    // "unmatched_labels" -- so without these both lists silently arrived
    // empty and every fill looked complete.
    [JsonPropertyName("not_filled")]
    public List<string> NotFilled { get; set; } = [];

    /// <summary>
    /// Inputs no stored pattern claimed. These are the labels the resolver
    /// would be asked about -- surfaced so a form that needs new patterns is
    /// visible rather than silently half-filled.
    /// </summary>
    [JsonPropertyName("unmatched_labels")]
    public List<string> UnmatchedLabels { get; set; } = [];
}
