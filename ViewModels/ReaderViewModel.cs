using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ComicReader.Config;
using ComicReader.Models;
using ComicReader.Services.Interfaces;
using System.Collections.Concurrent;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ComicReader.ViewModels;

/// <summary>
/// ViewModel controlling full-screen comic viewing, aggressive pre-buffering,
/// auto-resume reading progress, and fluid page navigation.
/// </summary>
public partial class ReaderViewModel : ObservableObject
{
    private readonly IComicDocumentService _documentService;
    private readonly IConfigurationService _configurationService;
    private readonly IProgressTrackingService _progressService;
    private readonly ConcurrentDictionary<int, ImageSource> _pageCache = new();
    private readonly SemaphoreSlim _preloadLock = new(1, 1);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Title))]
    private ComicBook? _currentComic;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanGoNext))]
    [NotifyPropertyChangedFor(nameof(CanGoPrevious))]
    [NotifyPropertyChangedFor(nameof(CurrentPageNumber))]
    [NotifyPropertyChangedFor(nameof(PageCounterText))]
    private int _currentPageIndex;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanGoNext))]
    [NotifyPropertyChangedFor(nameof(CanGoPrevious))]
    [NotifyPropertyChangedFor(nameof(PageCounterText))]
    [NotifyPropertyChangedFor(nameof(MaxPageNumber))]
    private int _totalPages;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PageCounterText))]
    private int _scrubberPageNumber = 1;

    [ObservableProperty]
    private ImageSource? _currentPageImage;

    [ObservableProperty]
    private ImageSource? _coverImageSource;

    [ObservableProperty]
    private bool _isLoadingDocument;

    [ObservableProperty]
    private string _loadingProgressText = "Opening comic...";

    [ObservableProperty]
    private bool _isLoadingPage;

    [ObservableProperty]
    private PageFitMode _fitMode = PageFitMode.FitToScreen;

    [ObservableProperty]
    private double _zoomFactor = 1.0;

    [ObservableProperty]
    private bool _isControlsOverlayVisible = true;

    [ObservableProperty]
    private int _animationDurationMs = 250;

    private bool _isSettingsOpen;
    public bool IsSettingsOpen
    {
        get => _isSettingsOpen;
        set => SetProperty(ref _isSettingsOpen, value);
    }

    [RelayCommand]
    public void OpenSettings()
    {
        OnPropertyChanged(nameof(DefaultFitMode));
        OnPropertyChanged(nameof(DefaultZoomFactor));
        IsSettingsOpen = true;
    }

    [RelayCommand]
    public void CloseSettings()
    {
        IsSettingsOpen = false;
    }

    public int MaxPageNumber => TotalPages > 0 ? TotalPages : 1;
    public string Title => CurrentComic?.Title ?? "Comic Reader";
    public int CurrentPageNumber => TotalPages > 0 ? CurrentPageIndex + 1 : 0;
    public string PageCounterText => TotalPages > 0 ? $"{ScrubberPageNumber} / {TotalPages}" : "0 / 0";
    public bool CanGoNext => CurrentPageIndex < TotalPages - 1;
    public bool CanGoPrevious => CurrentPageIndex > 0;

    /// <summary>
    /// Gets the list of available page fit modes for UI bindings.
    /// </summary>
    public IReadOnlyList<PageFitMode> AvailableFitModes { get; } = Enum.GetValues<PageFitMode>();

    /// <summary>
    /// Gets the list of available zoom levels for UI bindings.
    /// </summary>
    public IReadOnlyList<double> AvailableZoomLevels { get; } = [0.5, 0.75, 1.0, 1.25, 1.5, 1.75, 2.0, 2.5, 3.0];

    /// <summary>
    /// User-configured default fit mode applied when opening any comic.
    /// Persisted to user configuration and instantly applied to active reading view.
    /// </summary>
    public PageFitMode DefaultFitMode
    {
        get => _configurationService.Settings.Reader.DefaultFitMode;
        set
        {
            if (_configurationService.Settings.Reader.DefaultFitMode != value)
            {
                _configurationService.Settings.Reader.DefaultFitMode = value;
                _configurationService.SaveSettings();
                OnPropertyChanged();

                // Instantly apply preference to the active viewport if a comic is open
                SetFitMode(value);
            }
        }
    }

    /// <summary>
    /// User-configured default zoom factor applied when opening any comic.
    /// Persisted to user configuration and instantly applied to active reading view.
    /// </summary>
    public double DefaultZoomFactor
    {
        get => _configurationService.Settings.Reader.DefaultZoomFactor > 0 ? _configurationService.Settings.Reader.DefaultZoomFactor : 1.0;
        set
        {
            if (Math.Abs(_configurationService.Settings.Reader.DefaultZoomFactor - value) > 0.001)
            {
                _configurationService.Settings.Reader.DefaultZoomFactor = value;
                _configurationService.SaveSettings();
                OnPropertyChanged();

                // Instantly apply zoom to the active viewport if a comic is open
                if (CurrentComic != null)
                {
                    ZoomFactor = value;
                }
            }
        }
    }

    partial void OnCurrentPageIndexChanged(int value)
    {
        ScrubberPageNumber = value + 1;
    }

    partial void OnTotalPagesChanged(int value)
    {
        if (value > 0 && ScrubberPageNumber > value)
        {
            ScrubberPageNumber = value;
        }
    }

    public event Action? CloseRequested;

    public ReaderViewModel(
        IComicDocumentService documentService,
        IConfigurationService configurationService,
        IProgressTrackingService progressService)
    {
        _documentService = documentService;
        _configurationService = configurationService;
        _progressService = progressService;

        var readerSettings = _configurationService.Settings.Reader;
        AnimationDurationMs = readerSettings.PageAnimationDurationMs;
        FitMode = readerSettings.DefaultFitMode;
        ZoomFactor = readerSettings.DefaultZoomFactor > 0 ? readerSettings.DefaultZoomFactor : 1.0;
    }

    /// <summary>
    /// Opens a comic file, checks for saved progress to auto-resume, buffers surrounding pages,
    /// and displays the premium cinematic loading overlay.
    /// </summary>
    public async Task OpenComicAsync(ComicBook comic)
    {
        CurrentComic = comic;
        CurrentPageIndex = 0;
        TotalPages = 0;
        CurrentPageImage = null;
        ZoomFactor = _configurationService.Settings.Reader.DefaultZoomFactor > 0
            ? _configurationService.Settings.Reader.DefaultZoomFactor
            : 1.0;
        _pageCache.Clear();

        // Read DefaultFitMode from configuration and apply before first page renders
        FitMode = _configurationService.Settings.Reader.DefaultFitMode;

        IsLoadingDocument = true;
        LoadingProgressText = "Opening document...";

        // Set CoverImageSource for the frosted glass background
        if (!string.IsNullOrEmpty(comic.CoverCachePath) && File.Exists(comic.CoverCachePath))
        {
            try
            {
                var bi = new BitmapImage();
                bi.BeginInit();
                bi.UriSource = new Uri(Path.GetFullPath(comic.CoverCachePath));
                bi.CacheOption = BitmapCacheOption.OnLoad;
                bi.EndInit();
                bi.Freeze();
                CoverImageSource = bi;
            }
            catch
            {
                CoverImageSource = null;
            }
        }
        else
        {
            CoverImageSource = null;
        }

        try
        {
            // 1. Query saved progress for auto-resume
            var savedPageIndex = await _progressService.GetLastReadPageAsync(comic.FilePath);

            // 2. Open document and get page count
            TotalPages = await _documentService.OpenDocumentAsync(comic.FilePath);

            if (TotalPages > 0)
            {
                // Clamp saved index to valid bounds
                var resumeIndex = Math.Clamp(savedPageIndex, 0, TotalPages - 1);
                CurrentPageIndex = resumeIndex;

                LoadingProgressText = resumeIndex > 0
                    ? $"Resuming at page {resumeIndex + 1} of {TotalPages}..."
                    : "Buffering pages...";

                // 3. Aggressively pre-buffer the target resume page and surrounding pages in the background
                var targets = new List<int> { resumeIndex };
                if (resumeIndex + 1 < TotalPages) targets.Add(resumeIndex + 1);
                if (resumeIndex + 2 < TotalPages) targets.Add(resumeIndex + 2);
                if (resumeIndex - 1 >= 0) targets.Add(resumeIndex - 1);

                var prefetchTask = Task.Run(async () =>
                {
                    foreach (var target in targets)
                    {
                        try
                        {
                            var page = await _documentService.LoadPageAsync(target);
                            if (page?.PageImage != null)
                            {
                                _pageCache[target] = page.PageImage;
                            }
                        }
                        catch
                        {
                            // Continue buffering remaining targets
                        }
                    }
                });

                // Present a deliberate, cinematic loading experience (~1.6 seconds) while parsing & buffering
                var presentationDelay = Task.Delay(1600);
                await Task.WhenAll(prefetchTask, presentationDelay);

                if (CoverImageSource == null && _pageCache.TryGetValue(0, out var cover))
                {
                    CoverImageSource = cover;
                }

                // Set resume page from buffered cache
                if (_pageCache.TryGetValue(resumeIndex, out var targetPage))
                {
                    CurrentPageImage = targetPage;
                }
                else
                {
                    await LoadCurrentPageDirectAsync(resumeIndex);
                }

                // Kick off look-ahead pre-fetching for next pages
                _ = PreloadSurroundingPagesAsync(resumeIndex);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error opening comic: {ex.Message}");
        }
        finally
        {
            IsLoadingDocument = false;
        }
    }

    [RelayCommand]
    public async Task NextPageAsync()
    {
        if (CanGoNext)
        {
            CurrentPageIndex++;
            await DisplayPageAsync(CurrentPageIndex);
        }
    }

    [RelayCommand]
    public async Task PreviousPageAsync()
    {
        if (CanGoPrevious)
        {
            CurrentPageIndex--;
            await DisplayPageAsync(CurrentPageIndex);
        }
    }

    [RelayCommand]
    public async Task GoToPageAsync(int pageIndex)
    {
        if (pageIndex >= 0 && pageIndex < TotalPages && pageIndex != CurrentPageIndex)
        {
            CurrentPageIndex = pageIndex;
            await DisplayPageAsync(CurrentPageIndex);
        }
    }

    /// <summary>
    /// Commits the scrubber slider position when the user finishes dragging.
    /// Safely bounds-checks the target index, clears the screen to black, and asynchronously extracts the page.
    /// </summary>
    [RelayCommand]
    public async Task CommitScrubAsync()
    {
        if (TotalPages <= 0) return;

        int targetIndex = Math.Clamp(ScrubberPageNumber - 1, 0, TotalPages - 1);
        if (targetIndex != CurrentPageIndex)
        {
            await GoToPageAsync(targetIndex);
        }
        else
        {
            ScrubberPageNumber = CurrentPageIndex + 1;
        }
    }

    /// <summary>
    /// Displays the requested page. If in cache, displays instantly.
    /// If not in cache, immediately clears CurrentPageImage to reveal a seamless black space while decoding.
    /// Also dispatches debounced progress persistence.
    /// </summary>
    private async Task DisplayPageAsync(int pageIndex)
    {
        if (_pageCache.TryGetValue(pageIndex, out var cachedImage))
        {
            CurrentPageImage = cachedImage;
        }
        else
        {
            // Immediate source clearing: instantly clear old image to show seamless black space
            CurrentPageImage = null;
            IsLoadingPage = true;

            try
            {
                var page = await Task.Run(() => _documentService.LoadPageAsync(pageIndex));
                if (page?.PageImage != null)
                {
                    _pageCache[pageIndex] = page.PageImage;
                    CurrentPageImage = page.PageImage;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load page {pageIndex}: {ex.Message}");
            }
            finally
            {
                IsLoadingPage = false;
            }
        }

        // Auto-save reading progress with 500ms debouncing
        if (CurrentComic != null)
        {
            _progressService.UpdateProgress(CurrentComic.FilePath, pageIndex, TotalPages);
        }

        // Lookahead background preloading & memory pruning
        _ = PreloadSurroundingPagesAsync(pageIndex);
        PruneCache(pageIndex);
    }

    private async Task LoadCurrentPageDirectAsync(int pageIndex)
    {
        try
        {
            IsLoadingPage = true;
            CurrentPageImage = null;

            var page = await Task.Run(() => _documentService.LoadPageAsync(pageIndex));
            if (page?.PageImage != null)
            {
                _pageCache[pageIndex] = page.PageImage;
                CurrentPageImage = page.PageImage;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load page: {ex.Message}");
            CurrentPageImage = null;
        }
        finally
        {
            IsLoadingPage = false;
        }
    }

    /// <summary>
    /// Background task that aggressively loads adjacent pages ahead of the reader.
    /// </summary>
    private async Task PreloadSurroundingPagesAsync(int currentIndex)
    {
        if (!await _preloadLock.WaitAsync(0))
        {
            return; // Don't queue multiple preload threads
        }

        try
        {
            // High priority look-ahead targets: +1, +2, then -1
            var targets = new[] { currentIndex + 1, currentIndex + 2, currentIndex - 1 }
                .Where(idx => idx >= 0 && idx < TotalPages && !_pageCache.ContainsKey(idx));

            foreach (var target in targets)
            {
                try
                {
                    var page = await _documentService.LoadPageAsync(target);
                    if (page?.PageImage != null)
                    {
                        _pageCache[target] = page.PageImage;
                    }
                }
                catch
                {
                    // Continue with other targets
                }
            }
        }
        finally
        {
            _preloadLock.Release();
        }
    }

    /// <summary>
    /// Discards distant pages from the in-memory cache to prevent memory leaks during long reading sessions.
    /// </summary>
    private void PruneCache(int currentIndex)
    {
        const int cacheRadius = 6;
        var distantKeys = _pageCache.Keys
            .Where(k => Math.Abs(k - currentIndex) > cacheRadius)
            .ToList();

        foreach (var key in distantKeys)
        {
            _pageCache.TryRemove(key, out _);
        }
    }

    [RelayCommand]
    private void SetFitMode(PageFitMode mode)
    {
        FitMode = mode;
        ZoomFactor = 1.0;
    }

    [RelayCommand]
    public void UpdateDefaultFitMode(PageFitMode mode)
    {
        DefaultFitMode = mode;
    }

    [RelayCommand]
    private void ZoomIn()
    {
        ZoomFactor = Math.Min(3.0, Math.Round(ZoomFactor + 0.25, 2));
    }

    [RelayCommand]
    private void ZoomOut()
    {
        ZoomFactor = Math.Max(0.5, Math.Round(ZoomFactor - 0.25, 2));
    }

    [RelayCommand]
    private void ResetZoom()
    {
        ZoomFactor = 1.0;
    }

    [RelayCommand]
    private void ToggleControlsOverlay()
    {
        IsControlsOverlayVisible = !IsControlsOverlayVisible;
    }

    [RelayCommand]
    private void CloseReader()
    {
        // Flush any pending progress save immediately
        _ = _progressService.FlushAsync();

        _documentService.CloseDocument();
        _pageCache.Clear();

        CurrentComic = null;
        CurrentPageImage = null;
        CoverImageSource = null;
        TotalPages = 0;
        CurrentPageIndex = 0;

        CloseRequested?.Invoke();

        // Prompt optimized garbage collection when closing reader
        GC.Collect(1, GCCollectionMode.Optimized);
    }
}
