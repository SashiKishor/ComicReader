using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ComicReader.Services.Interfaces;
using Microsoft.Win32;
using System.IO;

namespace ComicReader.ViewModels;

/// <summary>
/// ViewModel controlling the first-time startup onboarding flow and initial root folder selection.
/// </summary>
public partial class StartupViewModel : ObservableObject
{
    private readonly IConfigurationService _configurationService;

    [ObservableProperty]
    private string _welcomeTitle = "Welcome to ComicReader";

    [ObservableProperty]
    private string _instructionText = "Please select your comic library folder to get started. ComicReader will scan your digital comics (.cbr, .cbz) and PDFs, organize them into categories, and remember your choice on future launches.";

    [ObservableProperty]
    private bool _isSelecting;

    [ObservableProperty]
    private string? _errorMessage;

    public event Action<string>? FolderSelected;

    public StartupViewModel(IConfigurationService configurationService)
    {
        _configurationService = configurationService;
    }

    [RelayCommand]
    public void SelectFolder()
    {
        try
        {
            IsSelecting = true;
            ErrorMessage = null;

            var dialog = new OpenFolderDialog
            {
                Title = "Select Your Comic Library Folder",
                Multiselect = false
            };

            var result = dialog.ShowDialog();
            if (result == true && !string.IsNullOrWhiteSpace(dialog.FolderName))
            {
                var selectedPath = dialog.FolderName;
                if (Directory.Exists(selectedPath))
                {
                    _configurationService.SaveRootDirectory(selectedPath);
                    FolderSelected?.Invoke(selectedPath);
                }
                else
                {
                    ErrorMessage = "The selected directory does not exist. Please try again.";
                }
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error selecting folder: {ex.Message}";
        }
        finally
        {
            IsSelecting = false;
        }
    }
}
