namespace Applr.Services.DTOs;

public sealed class ArbeitNowLinksDto
{
    public string First { get; set; } = string.Empty;

    public string? Last { get; set; }

    public string? Prev { get; set; }

    public string? Next { get; set; }
}
