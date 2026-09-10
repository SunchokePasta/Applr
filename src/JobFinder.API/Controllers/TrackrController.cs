using JobFinder.Services.DTOs;
using JobFinder.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace JobFinder.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class TrackrController : ControllerBase
{
    private readonly ITrackrScraperService _trackrScraperService;
    private readonly ILogger<TrackrController> _logger;

    public TrackrController(
        ITrackrScraperService trackrScraperService,
        ILogger<TrackrController> logger)
    {
        _trackrScraperService = trackrScraperService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ScrapedJobDto>>> Get(
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Trackr scraper endpoint was called.");

        var roles = await _trackrScraperService.GetRolesAsync(
            cancellationToken);

        return Ok(roles);
    }
}
