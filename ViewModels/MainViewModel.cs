using CommunityToolkit.Mvvm.ComponentModel;
using ComicReader.Config;
using ComicReader.Models;
using ComicReader.Services.Interfaces;

namespace ComicReader.ViewModels;

/// <summary>
/// Root navigation router coordinating between StartupViewModel, LibraryViewModel, and ReaderViewModel.
/// </summary>
public partial class MainViewModel : ObservableObject
{
    private readonly IConfigurationService _configurationService;
    private readonly StartupViewModel _startupViewModel;
    private readonly LibraryViewModel _libraryViewModel;
    private readonly ReaderViewModel _readerViewModel;

    [ObservableProperty]
    private ObservableObject _currentViewModel;

    public ThemeSettings Theme => _configurationService.Settings.Theme;

    public StartupViewModel Startup => _startupViewModel;
    public LibraryViewModel Library => _libraryViewModel;
    public ReaderViewModel Reader => _readerViewModel;

    public MainViewModel(
        IConfigurationService configurationService,
        StartupViewModel startupViewModel,
        LibraryViewModel libraryViewModel,
        ReaderViewModel readerViewModel)
    {
        _configurationService = configurationService;
        _startupViewModel = startupViewModel;
        _libraryViewModel = libraryViewModel;
        _readerViewModel = readerViewModel;

        // Wire routing transitions
        _startupViewModel.FolderSelected += OnFolderSelected;
        _libraryViewModel.ComicSelected += OnComicSelected;
        _readerViewModel.CloseRequested += OnReaderCloseRequested;

        // Router initialization: check if a root directory was previously saved
        var savedRoot = _configurationService.GetRootDirectory();
        if (!string.IsNullOrWhiteSpace(savedRoot))
        {
            _currentViewModel = _libraryViewModel;
            _ = _libraryViewModel.InitializeWithFolderAsync(savedRoot);
        }
        else
        {
            _currentViewModel = _startupViewModel;
        }
    }

    private async void OnFolderSelected(string folderPath)
    {
        CurrentViewModel = _libraryViewModel;
        await _libraryViewModel.InitializeWithFolderAsync(folderPath);
    }

    private async void OnComicSelected(ComicBook comic)
    {
        CurrentViewModel = _readerViewModel;
        await _readerViewModel.OpenComicAsync(comic);
    }

    private void OnReaderCloseRequested()
    {
        CurrentViewModel = _libraryViewModel;
        _libraryViewModel.RefreshProgress();
    }
}
