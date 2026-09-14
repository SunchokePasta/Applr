using Applr.Services.DTOs;
using Refit;

namespace Applr.Services.APIs;

public interface IArbeitNowApi
{
    [Get("")]
    Task<ArbeitNowResponseDto> GetJobsAsync(
        CancellationToken cancellationToken = default);
}
