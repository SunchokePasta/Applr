using Applr.Services.DTOs;
using Applr.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Applr.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class TrackrController : ControllerBase
{
    private readonly ITrackrService _trackrService;
    private readonly ILogger<TrackrController> _logger;

    public TrackrController(
        ITrackrService trackrService,
        ILogger<TrackrController> logger)
    {
        _trackrService = trackrService;
        _logger = logger;
    }

    [HttpGet("jobs/existing")]
    public async Task<ActionResult<IReadOnlyList<JobDto>>> GetExisting(
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Trackr existing endpoint was called.");

        var jobs = await _trackrService.GetExistingJobsAsync(cancellationToken);

        return Ok(jobs);
    }

    [HttpGet("jobs/new")]
    public async Task<ActionResult<IReadOnlyList<JobDto>>> GetNew(
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Trackr new endpoint was called.");

        var jobs = await _trackrService.GetNewJobsAsync(cancellationToken);

        return Ok(jobs);
    }

    [HttpPost("jobs/promote-new")]
    public async Task<IActionResult> PromoteNew(
        [FromBody] List<uint> jobIds,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Trackr promote-new endpoint was called for {JobCount} jobs.",
            jobIds.Count);

        await _trackrService.PromoteNewJobsAsync(jobIds, cancellationToken);

        return NoContent();
    }
}
