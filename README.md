# ComicReader - Local-First Digital Comic (.cbr, .cbz) & PDF Reader

[![.NET 8.0](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![WPF](https://img.shields.io/badge/UI-WPF-0078D7?logo=windows)](https://docs.microsoft.com/en-us/dotnet/desktop/wpf/)
[![MVVM](https://img.shields.io/badge/Architecture-Strict%20MVVM-brightgreen)](https://learn.microsoft.com/en-us/dotnet/communitytoolkit/mvvm/)
[![Platform](https://img.shields.io/badge/Platform-Windows%2010%20%2F%2011%20(x64)-blue)](https://www.microsoft.com/windows)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

**ComicReader** is a high-performance, local-first, offline Windows desktop application built with **C# / WPF (.NET 8)** adhering strictly to the **MVVM (Model-View-ViewModel)** architectural pattern. It delivers a **Netflix-styled library browsing interface** with hierarchical subfolder drill-down navigation, dynamic folder cover art peeking, a database-free reading progress tracking engine, and a fluid, immersive full-screen reader with auto-hiding controls and multi-axis mouse drag panning.

---

## Table of Contents
1. [Key Features](#key-features)
2. [Architectural Overview](#architectural-overview)
3. [Directory Structure](#directory-structure)
4. [Component Deep Dive](#component-deep-dive)
   - [Polymorphic Library Engine](#1-polymorphic-library-engine)
   - [Document Extraction & Stream Safety](#2-document-extraction--stream-safety)
   - [Database-Free Progress Tracking](#3-database-free-progress-tracking)
   - [Cinematic Loading & Page Transitions](#4-cinematic-loading--page-transitions)
   - [Immersive Auto-Hiding Reader & Multi-Axis Viewport](#5-immersive-auto-hiding-reader--multi-axis-viewport)
5. [Keyboard & Mouse Shortcuts](#keyboard--mouse-shortcuts)
6. [Building & Running](#building--running)
7. [Publishing for Release](#publishing-for-release)

---

## Key Features

- **Hierarchical Folder Navigation & Polymorphic Grid**: Displays subfolders and comic files side-by-side in a unified card grid. Click into any folder to drill down, complete with breadcrumbs and "Back to Parent Folder" navigation.
- **Dynamic Folder Cover Peeking**: Automatically peeks inside subfolders to discover the first nested comic and renders its cover art as the folder's poster card, tagged with a distinctive blue `📁 FOLDER` badge and item count.
- **Smart Reading Progress & Auto-Resume**: Remembers the exact last-read page for every comic using a lightweight, debounced JSON store in `AppData\Local\ComicReader`. Opening any comic instantly resumes where you left off.
- **Netflix-Style Visual Progress Bar**: Comic cards display a slim, flush red progress bar along their bottom edge indicating read completion percentage.
- **Cinematic Frosted Glass Loading Screen**: When opening a comic, a brief cinematic loading screen appears with a 50px Gaussian blurred cover art background, deep dark tint, 360-degree sweeping accent ring, and pulsing breathing pod that smoothly fades out via a DoubleAnimation Storyboard.
- **Immersive Full-Screen Reader with Auto-Hiding Controls**: Comic pages occupy the full window on a pitch-black canvas. The top header and bottom scrubber bar float above the image and remain hidden (`Opacity="0"`) until the mouse enters the top 60px or bottom 90px trigger zones, fading in and out smoothly.
- **Multi-Axis Scrolling & Mouse Drag Panning**: Full support for zoomed-in pages using `LayoutTransform` layout scaling. Click and drag the page with your mouse (`Hand` / `SizeAll` cursor) to pan naturally across both axes. Supports `Ctrl + Mouse Wheel` for zoom and `Shift + Mouse Wheel` for horizontal scrolling.
- **Page Transition Scroll Reset (No "Sticky Scroll")**: Switching pages instantly snaps the scroll viewport back to `(0, 0)` so new pages always start at the top-left corner.
- **Scrubber Slider with Drag-Completed Loading**: The scrubber is strictly bounded to `[1, TotalPages]`. Dragging the handle updates the page counter in real time without lagging the UI; page extraction triggers cleanly only when the user releases the slider handle.
- **Stream-Safe Archive & PDF Decoding**: Fully isolates ZIP/CBZ decompressor streams into rewound in-memory buffers before decoding with `BitmapCacheOption.OnLoad` and `.Freeze()`. Filters out non-image files (`ComicInfo.xml`, `thumbs.db`, directories, and macOS metadata).
- **Seamless Dark Window Chrome**: Client area extends directly into the Windows title bar with styled dark caption buttons, custom desktop icon (`Assets/app.ico`), and no title text or window frame borders.
- **Layered User Configuration**: Clean default `appsettings.json` bundled with the application; user directory selections and overrides are stored safely in `%LocalAppData%\ComicReader\usersettings.json`.

---

## Architectural Overview

```
┌─────────────────────────────────────────────────────────────┐
│                         Views (XAML)                        │
│   MainWindow.xaml   │   LibraryView.xaml   │ ReaderView.xaml│
│   StartupView.xaml  │   WindowChrome       │ Storyboards    │
└──────────────────────────────┬──────────────────────────────┘
                               │ Data Binding & RelayCommands
┌──────────────────────────────▼──────────────────────────────┐
│                      ViewModels (MVVM)                      │
│   MainViewModel     │   LibraryViewModel   │ ReaderViewModel│
│   StartupViewModel  │   FolderItemVM       │ ComicItemVM    │
└──────────────────────────────┬──────────────────────────────┘
                               │ Injected Service Contracts
┌──────────────────────────────▼──────────────────────────────┐
│                    Services (Core Engine)                   │
│   LibraryScannerService │ CoverExtractorService │ CacheSvc  │
│   ComicDocumentService  │ ProgressTrackingSvc   │ ConfigSvc │
└──────────────────────────────┬──────────────────────────────┘
                               │ Native Decoders & Archives
┌──────────────────────────────▼──────────────────────────────┐
│               Native / Third-Party Engines                  │
│       System.IO.Compression (CBZ) │ SharpCompress (CBR)     │
│       Docnet.Core / PDFium (PDF)  │ Windows StrCmpLogicalW  │
└─────────────────────────────────────────────────────────────┘
```

---

## Directory Structure

```
ComicReader/
├── comic_reader.csproj                     # .NET 8 WPF project file (win-x64)
├── appsettings.json                        # Central configuration defaults
├── .gitignore                              # Production .NET / WPF gitignore
├── App.xaml                                # Application styles, brushes & converters
├── App.xaml.cs                             # Composition root & ServiceProvider setup
├── AssemblyInfo.cs                         # WPF theme dictionary mapping
├── README.md                               # Project documentation
├── requirements.txt                        # Requirements & dependencies specification
│
├── Assets/                                 # Visual brand assets
│   └── app.ico                             # Multi-resolution application icon
│
├── Config/                                 # Strongly-typed configuration objects
│   ├── AppSettings.cs                      # Root settings aggregator
│   ├── LibrarySettings.cs                  # File extensions, concurrency, thumbnail bounds
│   ├── CacheSettings.cs                    # LocalAppData cache paths and limits
│   ├── ReaderSettings.cs                   # Animation durations and default fit modes
│   └── ThemeSettings.cs                    # Dark theme palette definition
│
├── Models/                                 # Pure domain entities & contracts
│   ├── ILibraryNode.cs                     # Base interface for polymorphic library grid items
│   ├── ComicBook.cs                        # Comic metadata (title, path, size, cover cache)
│   ├── ComicCategory.cs                    # Folder category grouping books
│   ├── ComicPage.cs                        # Decoded page bitmap, dimensions & entry key
│   ├── PageFitMode.cs                      # FitToScreen, FitToWidth, FitToHeight, OriginalSize
│   └── ReadingProgressEntry.cs             # Persisted read page index & total page count
│
├── Services/                               # Business logic & I/O engines
│   ├── Interfaces/
│   │   ├── IConfigurationService.cs        # Layered config provider contract
│   │   ├── ICacheService.cs                # SHA-256 disk cache contract
│   │   ├── ICoverExtractorService.cs       # Cover extraction contract (.cbr, .cbz, .pdf)
│   │   ├── IComicDocumentService.cs        # Multi-page archive and PDF loader contract
│   │   ├── ILibraryScannerService.cs       # Hierarchical directory discovery contract
│   │   └── IProgressTrackingService.cs     # Debounced progress tracking contract
│   ├── ConfigurationService.cs             # Reads appsettings.json and layers usersettings.json
│   ├── CacheService.cs                     # Deterministic SHA-256 disk cover cache
│   ├── CoverExtractorService.cs            # First-page thumbnail extractor with peeking
│   ├── ComicDocumentService.cs             # Thread-safe CBZ/CBR/PDF page decoder
│   ├── LibraryScannerService.cs            # Hierarchical directory scanner with folder peeking
│   └── ProgressTrackingService.cs          # Debounced JSON progress store with atomic writes
│
├── Utilities/                              # Helpers & Comparers
│   ├── BitmapHelper.cs                     # Stream-isolated, frozen WPF BitmapImage builder
│   └── NaturalStringComparer.cs            # Windows StrCmpLogicalW natural alphanumeric sorting
│
├── ViewModels/                             # Presentation logic (CommunityToolkit.Mvvm)
│   ├── MainViewModel.cs                    # Root navigation router (Startup -> Library -> Reader)
│   ├── StartupViewModel.cs                 # First-time onboarding & folder picker
│   ├── LibraryViewModel.cs                 # Hierarchical grid, drill-down & progress refresh
│   ├── FolderItemViewModel.cs              # Navigable folder node with peeked cover art
│   ├── ComicItemViewModel.cs               # Document file node with progress percentage
│   ├── ReaderViewModel.cs                  # Full-screen viewing, preloading & scrubber logic
│   ├── ComicCardViewModel.cs               # Card presentation helper
│   └── ComicCategoryViewModel.cs           # Category shelf helper
│
└── Views/                                  # WPF XAML views
    ├── MainWindow.xaml                     # Borderless dark WindowChrome shell
    ├── MainWindow.xaml.cs                  # Caption button event routing
    ├── StartupView.xaml                    # First-launch welcome & onboarding UI
    ├── LibraryView.xaml                    # Hierarchical grid with polymorphic DataTemplates
    ├── ReaderView.xaml                     # Full-screen reader with auto-hide hover controls
    └── ReaderView.xaml.cs                  # Panning, mouse gestures & keyboard shortcuts
```

---

## Component Deep Dive

### 1. Polymorphic Library Engine
- **`ILibraryNode` Contract**: Both `FolderItemViewModel` and `ComicItemViewModel` implement `ILibraryNode`.
- **Hierarchical Scanning (`LibraryScannerService`)**: When scanning a folder, immediate subfolders and comics are discovered side-by-side. The scanner peeks 1 level deep inside subfolders to find the first comic and extracts its cover for the folder card.
- **DataTemplates**: `LibraryView.xaml` uses polymorphic XAML `DataTemplate` resources mapped to `vm:FolderItemViewModel` (blue folder badge, item count) and `vm:ComicItemViewModel` (red format badge, file size, Netflix progress bar).

### 2. Document Extraction & Stream Safety
- **Native ZIP / CBZ**: Uses .NET's built-in `System.IO.Compression.ZipArchive`. Strictly filters out non-image files (`ComicInfo.xml`, `thumbs.db`), directories, and macOS metadata (`__MACOSX`, `._*`).
- **Stream Isolation**: SharpCompress and ZipArchive return non-seekable decompressor streams. `BitmapHelper.CreateImageSourceFromStream` copies bytes into an isolated `MemoryStream`, rewinds `Position = 0`, decodes with `BitmapCacheOption.OnLoad`, and calls `.Freeze()` before stream disposal.
- **PDFium Rasterization**: DocLib rasterizes PDF pages with an explicit `PageDimensions(1.5)` scaling factor directly into frozen 32-bit `Bgra32` bitmap buffers.

### 3. Database-Free Progress Tracking
- **Persistence Path**: Stored in `%LocalAppData%\ComicReader\reading_progress.json`.
- **Performance**: In-memory dictionary provides 0ms lookups during library rendering.
- **Debounced Disk Writes**: Updates during rapid page turning are debounced over 500ms and written atomically via temporary file replacement (`File.Move(..., overwrite: true)`).

### 4. Cinematic Loading & Page Transitions
- **Cover Frosted Glass**: The loading screen binds `CoverImageSource` with a 50px Gaussian blur and dark tint.
- **Continuous Animations**: Outer sweeping ring (360° rotation) and inner breathing pod (scale pulsing) run via XAML Storyboards.
- **Decoupled Page Turns**: Page turns slide smoothly using `RenderTransform` translation without re-triggering layout recalculation.

### 5. Immersive Auto-Hiding Reader & Multi-Axis Viewport
- **Pure XAML Auto-Hide**: Top header and bottom footer default to `Opacity="0.0"`. `DataTrigger` bindings on `IsMouseOver` of the 60px/90px trigger zones smoothly animate opacity in (200ms) and out (250ms).
- **LayoutTransform Scaling**: The comic image scales via `Border.LayoutTransform`, causing the `ScrollViewer`'s extent to expand naturally so horizontal scrollbars appear when zoomed in.
- **Mouse Drag Panning**: Clicking and dragging on zoomed-in pages captures the mouse and scrolls across both axes with responsive cursor feedback (`Hand` / `SizeAll`).

---

## Keyboard & Mouse Shortcuts

| Input | Action |
| :--- | :--- |
| **Right Arrow** / **Page Down** / **Spacebar** | Next Page |
| **Left Arrow** / **Page Up** / **Backspace** / **Shift + Space** | Previous Page |
| **Home** | First Page |
| **End** | Last Page |
| **Escape** | Exit Reader (Return to Library) |
| **Click & Drag** | Pan around page when zoomed in |
| **Ctrl + Mouse Wheel Up** | Zoom In |
| **Ctrl + Mouse Wheel Down** | Zoom Out |
| **Shift + Mouse Wheel** | Horizontal Scroll Left / Right |
| **Mouse Hover (Top / Bottom)** | Reveal Header / Scrubber Controls |

---

## Building & Running

### Prerequisites
- Windows 10 (Version 2004+) or Windows 11 (64-bit)
- Microsoft [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

### Clone & Build
```powershell
# Clone the repository
git clone https://github.com/your-username/ComicReader.git
cd ComicReader

# Restore dependencies and build
dotnet build -c Release -warnaserror

# Run the application
dotnet run -c Release
```

---

## Publishing for Release

To generate a standalone, optimized single-file executable for Windows x64:

```powershell
dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o ./publish
```

The output in `./publish` will include:
- `ComicReader.exe` (with embedded application icon and metadata)
- `appsettings.json` (clean default configuration)
- `runtimes/win-x64/native/pdfium.dll` (PDFium rendering engine)

---

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.
