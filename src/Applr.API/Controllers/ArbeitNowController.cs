using Applr.Services.DTOs;
using Applr.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Applr.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class ArbeitNowController : ControllerBase
{
    private readonly IArbeitNowService _arbeitNowService;
    private readonly ILogger<ArbeitNowController> _logger;

    public ArbeitNowController(
        IArbeitNowService arbeitNowService,
        ILogger<ArbeitNowController> logger)
    {
        _arbeitNowService = arbeitNowService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<ArbeitNowResponseDto>> Get(
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "ArbeitNow connection endpoint was called.");

        var response = await _arbeitNowService.GetJobsAsync(
            cancellationToken);

        return Ok(response);
    }
}
