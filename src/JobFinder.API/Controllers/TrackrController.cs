using JobFinder.Services.DTOs;
using JobFinder.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace JobFinder.API.Controllers;

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

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<DbJobDto>>> Get(
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Trackr endpoint was called.");

        var roles = await _trackrService.GetRolesAsync(
            cancellationToken);

        return Ok(roles);
    }
}
