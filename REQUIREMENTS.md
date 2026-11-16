# ComicFlix - System & Software Requirements Specification

This document outlines the formal technical, functional, non-functional, and environmental requirements for the **ComicFlix** digital comic and PDF reader application.

---

## 1. System & Environment Requirements

### 1.1 Supported Operating Systems
| Operating System | Architecture | Minimum Version | Recommended Version |
| :--- | :--- | :--- | :--- |
| **Windows 11** | x64, ARM64 (emulated) | 21H2 (Build 22000+) | 23H2 / 24H2 |
| **Windows 10** | x64 | Version 2004 (Build 19041+) | Version 22H2 |

> *Note: Windows 7, 8, and 8.1 are not supported due to .NET 8 WPF runtime constraints.*

### 1.2 Development & Build Prerequisites
- **.NET SDK**: .NET 8.0 SDK (Version `8.0.100` or higher; .NET 9 and .NET 10 SDKs supported via backward target compatibility)
- **Target Framework**: `net8.0-windows`
- **Compiler**: C# 12.0+
- **IDE Support**: Visual Studio 2022 (v17.8+), JetBrains Rider 2023.3+, or Visual Studio Code with the C# Dev Kit extension.

### 1.3 Minimum Hardware Specifications
- **Processor**: Dual-core x64 CPU @ 2.0 GHz or higher.
- **Memory (RAM)**: 4 GB minimum (8 GB recommended for large PDFs and dense image archives).
- **Storage**:
  - Application binary footprint: ~50 MB.
  - Cache allocation: User-configurable (default: up to 1 GB in `%LocalAppData%\ComicReader\Cache\Covers`).
- **Display Resolution**: 1280 x 720 minimum (1920 x 1080 or higher recommended).

---

## 2. Functional Requirements (FR)

### FR-1: Recursive Root Directory Scanning
- **FR-1.1**: The application must accept a root directory selected by the user via an OS native folder picker dialog (`Microsoft.Win32.OpenFolderDialog`).
- **FR-1.2**: The application must recursively scan all nested subdirectories for supported file formats.
- **FR-1.3**: The scan must execute asynchronously on a background thread without locking or freezing the UI.
- **FR-1.4**: The UI must display real-time scanning status and file discovery progress.

### FR-2: File Format Compatibility
- **FR-2.1**: Must support `.cbr` (Comic Book RAR) archives using stream-based extraction.
- **FR-2.2**: Must support `.cbz` (Comic Book ZIP) archives.
- **FR-2.3**: Must support `.pdf` (Portable Document Format) files.
- **FR-2.4**: Extensions must be configurable via `appsettings.json` without modifying application binaries.

### FR-3: Hierarchical Category Grouping (Netflix Shelves)
- **FR-3.1**: The folder structure must be preserved.
- **FR-3.2**: Files located in the root scan folder must be grouped into the primary library category.
- **FR-3.3**: Subfolders must be mapped into distinct category rows (shelves) reflecting their relative paths (e.g. `Marvel / Spider-Man / 2022`).
- **FR-3.4**: Within each category, comics must be sorted naturally using alphanumeric order (e.g., `Issue 1`, `Issue 2`, `Issue 10`).

### FR-4: Dynamic Cover Art Extraction
- **FR-4.1**: For `.cbr` and `.cbz` files, extract the first image entry (sorted naturally by filename).
- **FR-4.2**: For `.pdf` files, rasterize page index 0 to BGRA pixels and encode as JPEG.
- **FR-4.3**: Source covers exceeding the configured thumbnail dimensions (360x540) must be scaled down to conserve memory and disk usage.
- **FR-4.4**: Missing or corrupted archives must fail gracefully and show a placeholder cover with a loading/error state.

### FR-5: Deterministic Local Disk Caching
- **FR-5.1**: Covers must be cached locally to disk at `%LocalAppData%\ComicReader\Cache\Covers`.
- **FR-5.2**: Cache keys must be generated using a deterministic SHA-256 hash of the file path, last modified date, and file size.
- **FR-5.3**: Modifying or replacing a comic file must automatically produce a new hash, invalidating the old cache.
- **FR-5.4**: Existing cache hits must load directly from disk without re-opening archives.
- **FR-5.5**: Writes must use temporary files with atomic rename to prevent corrupted thumbnail files.

### FR-6: Immersive Comic Reader Viewer
- **FR-6.1**: Clicking any comic poster card must switch to a full-screen/maximized reader view.
- **FR-6.2**: The reader must render pages with high-quality bitmap scaling (`RenderOptions.BitmapScalingMode="HighQuality"`).
- **FR-6.3**: Pages must be decoded on-demand as requested, avoiding loading entire multi-hundred-page documents into memory.
- **FR-6.4**: Page turns must feature a smooth slide animation (quartic easing).

### FR-7: Display Fit Modes & Zoom
- **FR-7.1**: `FitToScreen`: Uniformly scales the page inside the current viewport (default).
- **FR-7.2**: `FitToWidth`: Scales to the viewport width, enabling vertical scrolling for tall pages or webtoons.
- **FR-7.3**: `FitToHeight`: Scales to viewport height.
- **FR-7.4**: `OriginalSize`: 1:1 pixel rendering with horizontal and vertical scrollbars.
- **FR-7.5**: Interactive zoom controls with zoom factors from 50% to 300%.

### FR-8: Navigation & Control Inputs
- **FR-8.1**: Next page: `Right Arrow`, `Space`, `PageDown`, or clicking the right edge of the screen.
- **FR-8.2**: Previous page: `Left Arrow`, `Shift + Space`, `PageUp`, `Backspace`, or clicking the left edge of the screen.
- **FR-8.3**: Exit reader: `Escape` key or "Back to Library" button.
- **FR-8.4**: Direct scrubbing: Interactive slider allowing users to jump directly to any page.
- **FR-8.5**: Toggle controls: `H` key or clicking the center of the page.

---

## 3. Non-Functional Requirements (NFR)

### NFR-1: Local-First & Offline Resilience
- **NFR-1.1**: The application must function 100% offline without requiring internet access, telemetry, or remote services.
- **NFR-1.2**: All dependencies must be bundled locally or embedded in the compiled binary.

### NFR-2: Performance & Concurrency
- **NFR-2.1**: UI frame rates must maintain 60 FPS during scanning and browsing.
- **NFR-2.2**: Thumbnail extraction must be throttled with a configurable degree of parallelism (`SemaphoreSlim`) to prevent disk I/O saturation.
- **NFR-2.3**: Bitmaps transferred to the UI must be frozen (`bitmap.Freeze()`) to enable cross-thread memory sharing and reduce garbage collection pauses.

### NFR-3: Memory & Resource Management
- **NFR-3.1**: When leaving the reader view, all open archive file streams, temporary byte arrays, and native PDFium handles (`IDocReader`) must be disposed immediately.
- **NFR-3.2**: Bitmap decoders must utilize `BitmapCacheOption.OnLoad` so intermediate memory streams are freed promptly.

### NFR-4: Configuration-Driven Design
- **NFR-4.1**: No file extensions, color codes, animation speeds, or folder paths may be hardcoded.
- **NFR-4.2**: The application must load all operational settings from `appsettings.json` on startup.

### NFR-5: User Experience & Aesthetic Standards
- **NFR-5.1**: The application must feature a modern, dark-themed Netflix aesthetic (`#141414` canvas, `#E50914` Netflix red accent).
- **NFR-5.2**: Custom minimalist scrollbars must replace default Windows scrollbars to eliminate visual clutter.

---

## 4. Package & Library Dependencies

| Package Name | Version | License | Technical Justification |
| :--- | :--- | :--- | :--- |
| **`CommunityToolkit.Mvvm`** | `8.4.0` | MIT | Official Microsoft MVVM toolkit providing source-generated `ObservableObject`, `[ObservableProperty]`, and `[RelayCommand]`. |
| **`Docnet.Core`** | `2.6.0` | Apache-2.0 | High-performance .NET wrapper around Google's PDFium C++ rasterization engine with bundled native binaries. |
| **`SharpCompress`** | `0.39.0` | MIT | Pure C# stream-based decompression library for `.cbr` (RAR), `.cbz` (ZIP), and 7z archives without requiring external binaries (e.g. `unrar.dll`). |
| **`Microsoft.Extensions.Configuration`** | `8.0.0` | MIT | Standard .NET configuration abstractions. |
| **`Microsoft.Extensions.Configuration.Json`** | `8.0.0` | MIT | JSON configuration provider for `appsettings.json`. |
| **`Microsoft.Extensions.Configuration.Binder`** | `8.0.2` | MIT | Strongly typed configuration model binding. |
| **`Microsoft.Extensions.DependencyInjection`** | `8.0.0` | MIT | Official Microsoft IoC / DI container for clean decoupling of services and ViewModels. |

---

## 5. Verification & Acceptance Criteria Matrix

| ID | Test Scenario | Acceptance Criteria |
| :--- | :--- | :--- |
| **TC-01** | Build & Compilation | `dotnet build` succeeds with zero errors on .NET 8 SDK. |
| **TC-02** | Folder Selection | Selecting a folder containing subfolders and comics creates horizontal shelf rows corresponding to subfolder names. |
| **TC-03** | CBR & PDF Discovery | Files ending with `.cbr`, `.cbz`, and `.pdf` are discovered; other file formats are excluded. |
| **TC-04** | Cover Extraction | First page of each file displays as a 2:3 aspect ratio card thumbnail. |
| **TC-05** | Cache Persistence | Re-scanning the same folder loads all covers instantly from disk without re-extracting. |
| **TC-06** | Fullscreen Reading | Clicking a card transitions to the reader; pages turn with a smooth slide animation. |
| **TC-07** | Fit Modes | Switching between "Fit Screen", "Fit Width", and "Fit Height" correctly reflows the page. |
| **TC-08** | Keyboard Hotkeys | Pressing `Space` or `Right Arrow` advances pages; `Esc` returns to library view. |

