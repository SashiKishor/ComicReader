using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

namespace ComicReader.ViewModels;

/// <summary>
/// ViewModel representing a single horizontal Netflix-style category shelf.
/// </summary>
public partial class ComicCategoryViewModel : ObservableObject
{
    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _relativePath = string.Empty;

    public ObservableCollection<ComicCardViewModel> Cards { get; } = [];

    public int ComicCount => Cards.Count;
}

