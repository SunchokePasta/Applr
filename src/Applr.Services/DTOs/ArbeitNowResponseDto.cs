namespace Applr.Services.DTOs;

public sealed class ArbeitNowResponseDto
{
    public List<ArbeitNowJobDto> Data { get; set; } = [];

    public ArbeitNowLinksDto Links { get; set; } = new();

    public ArbeitNowMetaDto Meta { get; set; } = new();
}
