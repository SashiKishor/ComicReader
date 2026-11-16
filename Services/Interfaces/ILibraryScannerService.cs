using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ComicReader.Models;

namespace ComicReader.Services.Interfaces;

/// <summary>
/// Service contract for scanning directories, discovering files and folders, and peeking for cover art.
/// </summary>
public interface ILibraryScannerService
{
    Task<IReadOnlyList<ILibraryNode>> ScanDirectoryContentsAsync(
        string directoryPath,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ComicCategory>> ScanDirectoryAsync(
        string rootDirectory,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default);
}
