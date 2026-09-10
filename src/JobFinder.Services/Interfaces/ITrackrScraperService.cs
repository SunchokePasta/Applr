using JobFinder.Services.DTOs;

namespace JobFinder.Services.Interfaces;

public interface ITrackrScraperService
{
    Task<IReadOnlyList<ScrapedJobDto>> GetRolesAsync(
        CancellationToken cancellationToken = default);
}

