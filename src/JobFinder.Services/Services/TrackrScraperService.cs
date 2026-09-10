using JobFinder.Services.APIs;
using JobFinder.Services.DTOs;
using JobFinder.Services.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

namespace JobFinder.Services.Services;

public sealed class TrackrScraperService : ITrackrScraperService
{
    private const string TrackrUrl =
        "https://app.the-trackr.com/uk-tech/summer-internships#sak9z8ouq4";

    private readonly ILogger<TrackrScraperService> _logger;

    public TrackrScraperService(
        ILogger<TrackrScraperService> logger)
    {
        _logger = logger;
    }

    public async Task<IReadOnlyList<ScrapedJobDto>> GetRolesAsync(
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Starting Trackr scraper.");

        using var playwright = await Playwright.CreateAsync();

        await using var browser =
            await playwright.Chromium.LaunchAsync(
                new BrowserTypeLaunchOptions
                {
                    Headless = true
                });

        var page = await browser.NewPageAsync();

        _logger.LogInformation(
            "Opening Trackr page: {Url}",
            TrackrUrl);

        await page.GotoAsync(
            TrackrUrl,
            new PageGotoOptions
            {
                WaitUntil = WaitUntilState.NetworkIdle
            });

        var rows = page.Locator("tr");

        await rows.First.WaitForAsync();

        var rowCount = await rows.CountAsync();

        _logger.LogInformation(
            "Found {RowCount} table rows.",
            rowCount);

        var ScrapedJobDtos = new List<ScrapedJobDto>();

        for (var i = 0; i < rowCount; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var row = rows.Nth(i);
            var cells = row.Locator("td");

            var cellCount = await cells.CountAsync();

            // Ignore header/empty/non-role rows.
            if (cellCount < 7)
            {
                continue;
            }

            var role = new ScrapedJobDto();

            // Status
            var statusInput = cells
                .Nth(0)
                .Locator("input");

            if (await statusInput.CountAsync() > 0)
            {
                role.Status =
                    (await statusInput
                        .GetAttributeAsync("value"))
                    ?.Trim();
            }

            // Company
            var companyLink = cells
                .Nth(1)
                .Locator("a");

            if (await companyLink.CountAsync() > 0)
            {
                role.CompanyName =
                    (await companyLink.InnerTextAsync())
                    .Trim();

                role.CompanyUrl =
                    await companyLink.GetAttributeAsync("href");
            }

            // Job
            var jobLink = cells
                .Nth(2)
                .Locator("a");

            if (await jobLink.CountAsync() > 0)
            {
                role.JobTitle =
                    (await jobLink.InnerTextAsync())
                    .Trim();

                role.JobUrl =
                    await jobLink.GetAttributeAsync("href");
            }

            // Dates
            role.CloseDate =
                (await cells.Nth(3).InnerTextAsync())
                .Trim();

            role.PostedDate =
                (await cells.Nth(4).InnerTextAsync())
                .Trim();

            var cvBadge = cells
                .Nth(5)
                .Locator("span[tooltip='CV required']");

            role.CvRequired =
                await cvBadge.CountAsync() > 0;

            var visaText =
                (await cells.Nth(6).InnerTextAsync())
                .Trim();

            role.VisaSponsorship =
                visaText.Equals(
                    "Yes",
                    StringComparison.OrdinalIgnoreCase);

            // Preserve raw cell contents.
            for (var j = 0; j < cellCount; j++)
            {
                var rawText =
                    (await cells.Nth(j).InnerTextAsync())
                    .Trim();

                if (!string.IsNullOrWhiteSpace(rawText))
                {
                    role.RawCells.Add(rawText);
                }
            }

            ScrapedJobDtos.Add(role);
        }

        _logger.LogInformation(
            "Scraped {RoleCount} roles from Trackr.",
            ScrapedJobDtos.Count);

        return ScrapedJobDtos;
    }
}
