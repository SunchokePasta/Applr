using Applr.Services.DTOs;

namespace Applr.Services.Interfaces;

public interface IArbeitNowService
{
    Task<ArbeitNowResponseDto> GetJobsAsync(
        CancellationToken cancellationToken = default);
}
