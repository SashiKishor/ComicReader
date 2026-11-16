using System;
using System.Threading;
using System.Threading.Tasks;
using ComicReader.Services.Interfaces;

namespace ComicReader.Models;

/// <summary>
/// Common contract for unified library items (folders and comic files) displayed in the main grid.
/// </summary>
public interface ILibraryNode
{
    string Title { get; }
    string FullPath { get; }
    bool IsFolder { get; }
    string? CoverPath { get; set; }
    bool IsLoadingCover { get; set; }
    string Subtitle { get; }
    string BadgeText { get; }
    string BadgeBackground { get; }
    double ProgressPercentage { get; }
    bool HasProgress { get; }

    Task LoadCoverAsync(ICoverExtractorService coverService, CancellationToken cancellationToken = default);
}
