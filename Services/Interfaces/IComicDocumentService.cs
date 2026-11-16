using ComicReader.Models;

namespace ComicReader.Services.Interfaces;

/// <summary>
/// Service contract for opening and navigating multi-page comic archives and PDFs.
/// </summary>
public interface IComicDocumentService : IDisposable
{
    Task<int> OpenDocumentAsync(string filePath, CancellationToken cancellationToken = default);
    Task<ComicPage?> LoadPageAsync(int pageIndex, CancellationToken cancellationToken = default);
    void CloseDocument();
}

