using Applr.Services.DTOs;

namespace Applr.DesktopServices.Interfaces;

/// <summary>
/// Talks to Applr.API's TrackrController (api/Trackr/...), which itself
/// wraps Applr.RestApi's jobs/existing + jobs/new + jobs/promote-new.
///
/// The on-load flow the desktop app drives is: GetExisting + GetNew in
/// parallel, then (silently) PromoteNew on whatever just came back as
/// New. It no longer starts with a DbSync call -- raw_jobs is synced
/// into jobs by TrackrScraper/Scraper.py, which posts to Applr.RestApi
/// itself right after each scrape. By the time this app opens, the sync
/// has already happened; this just reads what's there.
///
/// Every method throws ApplrApiException and nothing else. The message
/// on it is already phrased for a user and the reference on it already
/// appears in a log file, so callers never need to inspect a status
/// code or decide what is safe to display.
/// </summary>
public interface IApplrClient
{
    /// <summary>jobs where status == "Unreviewed".</summary>
    Task<IReadOnlyList<JobDto>> GetExistingJobsAsync(CancellationToken cancellationToken = default);

    /// <summary>jobs where status == "New".</summary>
    Task<IReadOnlyList<JobDto>> GetNewJobsAsync(CancellationToken cancellationToken = default);

    /// <summary>Flips the given job ids from 'New' to 'Unreviewed'.</summary>
    Task PromoteNewJobsAsync(
        IReadOnlyList<uint> jobIds,
        CancellationToken cancellationToken = default);
}
