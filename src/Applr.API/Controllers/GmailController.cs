using Applr.Services.DTOs;
using Applr.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Applr.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class GmailController : ControllerBase
{
    private readonly IGmailService _gmailService;
    private readonly ILogger<GmailController> _logger;

    public GmailController(
        IGmailService gmailService,
        ILogger<GmailController> logger)
    {
        _gmailService = gmailService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<List<GmailMessageSummaryDto>>> Get(
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Gmail latest-messages endpoint was called.");

        var messages = await _gmailService.GetLatestMessagesAsync(
            cancellationToken: cancellationToken);

        return Ok(messages);
    }
}
