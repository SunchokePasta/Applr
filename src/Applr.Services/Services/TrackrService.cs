using Applr.Services.APIs;
using Applr.Services.DTOs;
using Applr.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace Applr.Services.Services;

/// <summary>
/// Used to scrape Trackr directly with Playwright. Now just calls
/// Applr.RestApi's DB-backed jobs endpoints -- same shape as
/// ArbeitNowService calling IArbeitNowApi. The scraping itself lives in
/// the separate Python job (TrackrScraper/Scraper.py) that upserts into
/// raw_jobs, the table Applr.RestApi reads from.
///
/// Scraper.py also triggers the raw_jobs -> jobs sync itself, straight
/// against Applr.RestApi, which is why there is no DbSyncAsync here:
/// this service is the desktop app's read path, not a gateway for
/// everything that talks to the REST API.
///
/// No try/catch anywhere in this class on purpose. A Refit failure
/// travelling out of here is caught, logged and translated once by
/// Applr.API's ExceptionHandlingMiddleware; catching it here as well
/// would put the same failure in the log twice.
/// </summary>
public sealed class TrackrService : ITrackrService
{
    private readonly ITrackrRestApi _trackrRestApi;
    private readonly ILogger<TrackrService> _logger;

    public TrackrService(
        ITrackrRestApi trackrRestApi,
        ILogger<TrackrService> logger)
    {
        _trackrRestApi = trackrRestApi;
        _logger = logger;
    }

    public async Task<IReadOnlyList<JobDto>> GetExistingJobsAsync(
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Requesting existing jobs from Applr.RestApi.");

        var jobs = await _trackrRestApi.GetExistingJobsAsync(cancellationToken);

        _logger.LogInformation(
            "Received {JobCount} existing jobs from Applr.RestApi.",
            jobs.Count);

        return jobs;
    }

    public async Task<IReadOnlyList<JobDto>> GetNewJobsAsync(
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Requesting new jobs from Applr.RestApi.");

        var jobs = await _trackrRestApi.GetNewJobsAsync(cancellationToken);

        _logger.LogInformation(
            "Received {JobCount} new jobs from Applr.RestApi.",
            jobs.Count);

        return jobs;
    }

    public async Task PromoteNewJobsAsync(
        IReadOnlyList<uint> jobIds,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Requesting Applr.RestApi to promote {JobCount} new jobs to Unreviewed.",
            jobIds.Count);

        await _trackrRestApi.PromoteNewJobsAsync(
            new PromoteNewJobsRequest { JobIds = jobIds.ToList() },
            cancellationToken);
    }
}
