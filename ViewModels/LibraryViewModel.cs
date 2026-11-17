using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ComicReader.Config;
using ComicReader.Models;
using ComicReader.Services.Interfaces;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.IO;

namespace ComicReader.ViewModels;

/// <summary>
/// ViewModel driving hierarchical folder navigation, unified grid items (folders + comics), and Netflix-style browsing.
/// </summary>
public partial class LibraryViewModel : ObservableObject
{
    private readonly ILibraryScannerService _scannerService;
    private readonly ICoverExtractorService _coverService;
    private readonly IConfigurationService _configService;
    private readonly IProgressTrackingService _progressService;
    private CancellationTokenSource? _scanCts;

    [ObservableProperty]
    private string _rootDirectory = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CurrentFolderName))]
    private string _currentDirectory = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(NavigateUpCommand))]
    private bool _canNavigateUp;

    [ObservableProperty]
    private bool _isScanning;

    [ObservableProperty]
    private bool _hasNoItemsFound;

    [ObservableProperty]
    private string _statusMessage = "Ready.";

    [ObservableProperty]
    private int _totalItemsCount;

    [ObservableProperty]
    private string _searchFilter = string.Empty;

    private bool _isSettingsOpen;
    public bool IsSettingsOpen
    {
        get => _isSettingsOpen;
        set => SetProperty(ref _isSettingsOpen, value);
    }

    public IReadOnlyList<PageFitMode> AvailableFitModes { get; } = Enum.GetValues<PageFitMode>();
    public IReadOnlyList<double> AvailableZoomLevels { get; } = [0.5, 0.75, 1.0, 1.25, 1.5, 1.75, 2.0, 2.5, 3.0];

    public PageFitMode DefaultFitMode
    {
        get => _configService.Settings.Reader.DefaultFitMode;
        set
        {
            if (_configService.Settings.Reader.DefaultFitMode != value)
            {
                _configService.Settings.Reader.DefaultFitMode = value;
                _configService.SaveSettings();
                OnPropertyChanged();
            }
        }
    }

    public double DefaultZoomFactor
    {
        get => _configService.Settings.Reader.DefaultZoomFactor > 0 ? _configService.Settings.Reader.DefaultZoomFactor : 1.0;
        set
        {
            if (Math.Abs(_configService.Settings.Reader.DefaultZoomFactor - value) > 0.001)
            {
                _configService.Settings.Reader.DefaultZoomFactor = value;
                _configService.SaveSettings();
                OnPropertyChanged();
            }
        }
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

    public string CurrentFolderName =>
        string.IsNullOrWhiteSpace(CurrentDirectory)
            ? "Library"
            : (Path.GetFileName(CurrentDirectory.TrimEnd('\\', '/')) is { Length: > 0 } name ? name : CurrentDirectory);

    public ObservableCollection<ILibraryNode> Items { get; } = [];

    public ObservableCollection<ComicCategoryViewModel> Categories { get; } = [];

    public event Action<ComicBook>? ComicSelected;

    public LibraryViewModel(
        ILibraryScannerService scannerService,
        ICoverExtractorService coverService,
        IConfigurationService configService,
        IProgressTrackingService progressService)
    {
        _scannerService = scannerService;
        _coverService = coverService;
        _configService = configService;
        _progressService = progressService;
    }

    /// <summary>
    /// Refreshes in-memory reading progress on all loaded comics (e.g. when returning from ReaderView).
    /// </summary>
    public void RefreshProgress()
    {
        foreach (var node in Items)
        {
            if (node is ComicItemViewModel comic)
            {
                var progress = _progressService.GetProgress(comic.FullPath);
                if (progress != null && progress.TotalPages > 0)
                {
                    comic.UpdateProgress(progress.LastReadPageIndex, progress.TotalPages);
                }
            }
        }
    }

    /// <summary>
    /// Initial entry point from startup or router.
    /// </summary>
    public async Task InitializeWithFolderAsync(string rootPath)
    {
        RootDirectory = rootPath;
        await NavigateToDirectoryAsync(rootPath);
    }

    /// <summary>
    /// Backwards compatible method for scanning the root library folder.
    /// </summary>
    public async Task ScanLibraryAsync(string rootPath)
    {
        await InitializeWithFolderAsync(rootPath);
    }

    /// <summary>
    /// Drills down into a specific folder, clearing the grid and loading immediate contents.
    /// </summary>
    public async Task NavigateToDirectoryAsync(string directoryPath)
    {
        if (string.IsNullOrWhiteSpace(directoryPath) || !Directory.Exists(directoryPath))
        {
            StatusMessage = "Invalid directory path.";
            HasNoItemsFound = true;
            return;
        }

        try
        {
            _scanCts?.Cancel();
            _scanCts?.Dispose();
        }
        catch
        {
            // Ignore cancellation disposal race
        }

        _scanCts = new CancellationTokenSource();
        var ct = _scanCts.Token;

        try
        {
            IsScanning = true;
            CurrentDirectory = Path.GetFullPath(directoryPath);
            UpdateCanNavigateUp();

            Items.Clear();
            TotalItemsCount = 0;
            HasNoItemsFound = false;
            StatusMessage = $"Loading '{CurrentFolderName}'...";

            var progress = new Progress<string>(msg => StatusMessage = msg);
            var discoveredNodes = await _scannerService.ScanDirectoryContentsAsync(CurrentDirectory, progress, ct);

            foreach (var node in discoveredNodes)
            {
                Items.Add(node);
            }

            TotalItemsCount = Items.Count;
            HasNoItemsFound = Items.Count == 0;
            StatusMessage = Items.Count == 0
                ? "This folder is empty."
                : $"Loaded {Items.Count} items in '{CurrentFolderName}'.";

            // Asynchronously stream cover extractions in the background for any pending items
            _ = LoadCoversConcurrentlyAsync(discoveredNodes, ct);
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Navigation cancelled.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsScanning = false;
        }
    }

    /// <summary>
    /// Command invoked when an item card (folder or comic) is clicked in the grid.
    /// </summary>
    [RelayCommand]
    private async Task OpenNodeAsync(ILibraryNode? node)
    {
        if (node == null) return;

        if (node is FolderItemViewModel folder)
        {
            // Drill down into subfolder
            await NavigateToDirectoryAsync(folder.FullPath);
        }
        else if (node is ComicItemViewModel comic)
        {
            // Open reader for this comic
            ComicSelected?.Invoke(comic.Book);
        }
    }

    /// <summary>
    /// Direct command to open a comic file.
    /// </summary>
    [RelayCommand]
    private void OpenComic(ComicBook? book)
    {
        if (book != null)
        {
            ComicSelected?.Invoke(book);
        }
    }

    /// <summary>
    /// Navigates up one level in the folder hierarchy.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanNavigateUp))]
    public async Task NavigateUpAsync()
    {
        if (!CanNavigateUp || string.IsNullOrWhiteSpace(CurrentDirectory)) return;

        try
        {
            var parent = Directory.GetParent(CurrentDirectory);
            if (parent != null && parent.Exists)
            {
                await NavigateToDirectoryAsync(parent.FullName);
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Cannot navigate up: {ex.Message}";
        }
    }

    /// <summary>
    /// Allows the user to select a new root folder via native Windows picker.
    /// </summary>
    [RelayCommand]
    public async Task ChangeFolderAsync()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Select Comic Library Folder",
            Multiselect = false
        };

        if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.FolderName))
        {
            if (Directory.Exists(dialog.FolderName))
            {
                RootDirectory = dialog.FolderName;
                _configService.SaveRootDirectory(dialog.FolderName);
                await NavigateToDirectoryAsync(dialog.FolderName);
            }
        }
    }

    /// <summary>
    /// Reloads the current directory contents.
    /// </summary>
    [RelayCommand]
    public async Task RefreshAsync()
    {
        if (!string.IsNullOrWhiteSpace(CurrentDirectory) && Directory.Exists(CurrentDirectory))
        {
            await NavigateToDirectoryAsync(CurrentDirectory);
        }
        else if (!string.IsNullOrWhiteSpace(RootDirectory) && Directory.Exists(RootDirectory))
        {
            await NavigateToDirectoryAsync(RootDirectory);
        }
    }

    private void UpdateCanNavigateUp()
    {
        if (string.IsNullOrWhiteSpace(CurrentDirectory) || string.IsNullOrWhiteSpace(RootDirectory))
        {
            CanNavigateUp = false;
            return;
        }

        try
        {
            var normCurrent = Path.TrimEndingDirectorySeparator(Path.GetFullPath(CurrentDirectory));
            var normRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(RootDirectory));

            CanNavigateUp = !string.Equals(normCurrent, normRoot, StringComparison.OrdinalIgnoreCase)
                            && Directory.GetParent(CurrentDirectory) != null;
        }
        catch
        {
            CanNavigateUp = false;
        }
    }

    private async Task LoadCoversConcurrentlyAsync(IReadOnlyList<ILibraryNode> nodes, CancellationToken ct)
    {
        var pendingNodes = nodes.Where(n => string.IsNullOrEmpty(n.CoverPath)).ToList();
        if (pendingNodes.Count == 0) return;

        var maxParallel = _configService.Settings.Library.MaxDegreeOfParallelism;
        using var semaphore = new SemaphoreSlim(Math.Max(1, maxParallel));

        var tasks = pendingNodes.Select(async node =>
        {
            await semaphore.WaitAsync(ct);
            try
            {
                if (!ct.IsCancellationRequested)
                {
                    await node.LoadCoverAsync(_coverService, ct);
                }
            }
            catch
            {
                // Graceful fallback for single item failure
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(tasks);
    }
}
