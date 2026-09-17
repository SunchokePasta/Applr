using Applr.Services.DTOs;
using Refit;

namespace Applr.Services.APIs;

/// <summary>
/// The slice of Applr.RestApi the desktop app needs.
///
/// Deliberately does NOT declare raw-jobs/db-sync. Syncing raw_jobs into
/// jobs is triggered by TrackrScraper/Scraper.py posting to
/// Applr.RestApi directly, right after it upserts raw_jobs -- so the
/// sync runs on the scraper's schedule rather than only when this app
/// happens to be open, and an unattended scrape needs one .NET process
/// alive instead of two.
/// </summary>
public interface ITrackrRestApi
{
    [Get("/jobs/existing")]
    Task<List<JobDto>> GetExistingJobsAsync(CancellationToken cancellationToken = default);

    [Get("/jobs/new")]
    Task<List<JobDto>> GetNewJobsAsync(CancellationToken cancellationToken = default);

    [Post("/jobs/promote-new")]
    Task PromoteNewJobsAsync(
        [Body] PromoteNewJobsRequest request,
        CancellationToken cancellationToken = default);
}
