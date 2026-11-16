namespace ComicReader.Models;

/// <summary>
/// Domain model representing a comic or document file in the library.
/// </summary>
public sealed class ComicBook
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Title { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string DirectoryPath { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public string RelativeSubfolder { get; set; } = string.Empty;
    public string FileExtension { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public DateTime LastModifiedUtc { get; set; }
    public int? TotalPages { get; set; }
    public string? CoverCachePath { get; set; }
}

