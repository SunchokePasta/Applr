using JobFinder.Services.DTOs;

namespace JobFinder.Services.Interfaces;

public interface ITrackrService
{
    Task<IReadOnlyList<DbJobDto>> GetRolesAsync(
        CancellationToken cancellationToken = default);
}
