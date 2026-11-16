using CommunityToolkit.Mvvm.ComponentModel;
using ComicReader.Models;
using ComicReader.Services.Interfaces;

namespace ComicReader.ViewModels;

/// <summary>
/// ViewModel representing an individual comic card thumbnail in the Netflix-style library row.
/// </summary>
public partial class ComicCardViewModel : ObservableObject
{
    private readonly ComicBook _book;

    [ObservableProperty]
    private string? _coverPath;

    [ObservableProperty]
    private bool _isLoadingCover = true;

    public ComicBook Book => _book;
    public string Title => _book.Title;
    public string FilePath => _book.FilePath;
    public string CategoryName => _book.CategoryName;
    public string ExtensionBadge => _book.FileExtension.TrimStart('.').ToUpperInvariant();

    public string FileSizeFormatted
    {
        get
        {
            const double mb = 1024 * 1024;
            return $"{_book.FileSizeBytes / mb:F1} MB";
        }
    }

    public ComicCardViewModel(ComicBook book)
    {
        _book = book;
        _coverPath = book.CoverCachePath;
        _isLoadingCover = string.IsNullOrEmpty(_coverPath);
    }

    public async Task LoadCoverAsync(ICoverExtractorService coverExtractorService, CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrEmpty(CoverPath))
        {
            IsLoadingCover = false;
            return;
        }

        try
        {
            IsLoadingCover = true;
            var path = await coverExtractorService.GetOrCreateCoverAsync(_book.FilePath, cancellationToken);
            CoverPath = path;
            _book.CoverCachePath = path;
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

