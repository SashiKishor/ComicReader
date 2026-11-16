using CommunityToolkit.Mvvm.ComponentModel;
using ComicReader.Models;
using ComicReader.Services.Interfaces;
using System.IO;

namespace ComicReader.ViewModels;

/// <summary>
/// ViewModel representing a navigable subfolder item in the library grid.
/// </summary>
public partial class FolderItemViewModel : ObservableObject, ILibraryNode
{
    private readonly DirectoryInfo _directory;

    [ObservableProperty]
    private string? _coverPath;

    [ObservableProperty]
    private bool _isLoadingCover;

    [ObservableProperty]
    private int _itemCount;

    public DirectoryInfo Directory => _directory;
    public string Title => _directory.Name;
    public string FullPath => _directory.FullName;
    public bool IsFolder => true;
    public string BadgeText => "FOLDER";
    public string BadgeBackground => "#2563EB"; // Vibrant Modern Blue

    public string? FirstComicFilePath { get; set; }

    public double ProgressPercentage => 0.0;
    public bool HasProgress => false;

    public string Subtitle => ItemCount == 1 ? "1 item" : $"{ItemCount} items";

    public FolderItemViewModel(DirectoryInfo directory, string? firstComicFilePath, int itemCount, string? coverPath = null)
    {
        _directory = directory;
        FirstComicFilePath = firstComicFilePath;
        _itemCount = itemCount;
        _coverPath = coverPath;
        _isLoadingCover = string.IsNullOrEmpty(coverPath) && !string.IsNullOrEmpty(firstComicFilePath);
    }

    public async Task LoadCoverAsync(ICoverExtractorService coverService, CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrEmpty(CoverPath))
        {
            IsLoadingCover = false;
            return;
        }

        if (string.IsNullOrEmpty(FirstComicFilePath))
        {
            IsLoadingCover = false;
            return;
        }

        try
        {
            IsLoadingCover = true;
            var path = await coverService.GetOrCreateCoverAsync(FirstComicFilePath, cancellationToken);
            CoverPath = path;
        }
        catch
        {
            CoverPath = null;
        }
        finally
        {
            IsLoadingCover = false;
        }
    }
}
