using Applr.Services.DTOs;

namespace Applr.DesktopServices.Interfaces;

public interface IApplrClient
{
    Task<IReadOnlyList<DbJobDto>> GetJobsAsync(
        CancellationToken cancellationToken = default);
}
