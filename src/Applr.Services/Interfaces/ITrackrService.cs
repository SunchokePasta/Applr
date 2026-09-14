using Applr.Services.DTOs;

namespace Applr.Services.Interfaces;

public interface ITrackrService
{
    Task<IReadOnlyList<DbJobDto>> GetRolesAsync(
        CancellationToken cancellationToken = default);
}
