namespace Applr.Services.DTOs;

public sealed class ArbeitNowMetaDto
{
    public int CurrentPage { get; set; }

    public int From { get; set; }

    public string Path { get; set; } = string.Empty;

    public int PerPage { get; set; }

    public int To { get; set; }

    public string Terms { get; set; } = string.Empty;

    public string Info { get; set; } = string.Empty;
}
