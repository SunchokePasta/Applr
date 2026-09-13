using JobFinder.Services.APIs;
using JobFinder.Services.DTOs;
using JobFinder.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace JobFinder.Services.Services;

/// <summary>
/// Used to scrape Trackr directly with Playwright. Now just calls
/// Applr.RestApi's DB-backed jobs endpoint -- same shape as
/// ArbeitNowService calling IArbeitNowApi. The scraping itself lives in
/// the separate Python job (TrackrScraper/Scraper.py) that upserts into
/// the database Applr.RestApi reads from.
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

    public async Task<IReadOnlyList<DbJobDto>> GetRolesAsync(
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Requesting jobs from Applr.RestApi.");

        var jobs = await _trackrRestApi.GetAllJobsAsync(cancellationToken);

        _logger.LogInformation(
            "Received {JobCount} jobs from Applr.RestApi.",
            jobs.Count);

        return jobs;
    }
}
