using System.Collections.ObjectModel;

namespace ComicReader.Models;

/// <summary>
/// Represents a row or genre-like category grouping comic books from subfolders.
/// </summary>
public sealed class ComicCategory
{
    public string Name { get; set; } = string.Empty;
    public string RelativePath { get; set; } = string.Empty;
    public ObservableCollection<ComicBook> Books { get; set; } = [];
}

