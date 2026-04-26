# SpaceSniffer — WPF Treemap Disk Space Analyzer

## Overview

A WPF (.NET 9) disk space analyzer using the **Squarified Treemap** visualization. Users select folders, drives, or all drives to scan; results are shown as nested, color-coded rectangles where each rectangle's area is proportional to the file/folder size.

## Technology Stack

- **.NET 9.0-windows** with WPF
- **CommunityToolkit.Mvvm** — ObservableObject, RelayCommand, ObservableCollection
- **Custom TreemapControl** — DrawingVisual / DrawingContext render

## Project Structure

```
SpaceSniffer/
├── App.xaml / App.xaml.cs
├── MainWindow.xaml / MainWindow.xaml.cs        # Menu bar, treemap host, status bar
├── ViewModels/
│   └── MainViewModel.cs                        # App state, commands, navigation
├── Models/
│   └── FileNode.cs                             # File/folder tree node
├── Controls/
│   ├── TreemapControl.cs                       # Custom treemap rendering control
│   └── TreemapLayout.cs                        # Squarified Treemap algorithm
├── Services/
│   ├── DiskScanner.cs                          # Async recursive disk scanner
│   └── ElevationService.cs                     # UAC elevation / restart
└── Converters/
    └── SizeFormatConverter.cs                  # Bytes → human-readable
```

## FileNode Model

```csharp
public class FileNode : ObservableObject {
    public string Name { get; set; }
    public string FullPath { get; set; }
    public long Size { get; set; }
    public FileNodeType Type { get; set; }       // Folder / File
    public ObservableCollection<FileNode> Children { get; set; }
    public double SizeRatio { get; set; }        // % of parent size
}
```

## MainViewModel

```csharp
public class MainViewModel : ObservableObject {
    // State
    public FileNode CurrentRoot { get; set; }
    public Stack<FileNode> NavigationHistory { get; set; }
    public bool IsScanning { get; set; }
    public string CurrentPath { get; set; }
    public int ScanProgressPercent { get; set; }
    public string ScanProgressText { get; set; }

    // Commands
    public RelayCommand SelectFolderCommand { get; }
    public RelayCommand SelectDriveCommand { get; }
    public RelayCommand ScanAllDrivesCommand { get; }
    public RelayCommand<FileNode> NavigateToCommand { get; }    // zoom in
    public RelayCommand GoBackCommand { get; }
    public RelayCommand CancelScanCommand { get; }
}
```

## Menu Structure

- **File** → Select Folder...     (opens FolderBrowserDialog)
- **File** → Select Drive...      (lists logical drives)
- **File** → Scan All Drives      (scans all drives, aggregates)
- **File** → Exit
- **Help** → About

## Squarified Treemap Algorithm

Implementation of the Bruls-Huizing-van Wijk squarified treemap algorithm:

1. Sort items by size descending
2. Work from the longer side of the rectangle
3. Add items row-by-row; after each addition, check aspect ratio
4. If aspect ratio worsens, start a new row
5. Recursively apply to child nodes within each parent rectangle

**Layout input:** `IEnumerable<FileNode>` (siblings to lay out in a Rect)
**Layout output:** `Dictionary<FileNode, Rect>` (positioned rectangles)
**Recursive for hierarchy**: each folder node gets its own layout call with its children

## TreemapControl

- Inherits `FrameworkElement`
- **Dependency Properties:** `ItemsSource` (FileNode), `SelectedNode` (FileNode)
- **Rendering:** Override `OnRender(DrawingContext)`, draw filled rectangles + text labels
- **Hit testing:** `MouseMove` / `MouseLeftButtonDown` → coordinate-to-rect lookup
- **ToolTip:** Shows `Name`, `Size` (formatted), `SizeRatio` on hover
- **Zoom:** Click folder → triggers NavigateTo command → re-layout with clicked node as root
- **Color scheme:**
  - Folders: warm palette (oranges, browns)
  - Files: cool palette (blues, teals)
  - Deeper nesting, slightly lighter shade

## Disk Scanning

- `DiskScanner.ScanAsync(string path, CancellationToken ct)`
- Uses `Directory.EnumerateFileSystemEntries` for enumeration
- Parallel folder scanning with concurrency limit
- `UnauthorizedAccessException` → skip silently
- Builds `FileNode` tree bottom-up (children compute total size for parent)
- Reports progress via `IProgress<(int percent, string currentPath)>`

## Permission Handling (UAC)

1. App runs as normal user
2. If scanning encounters access-denied on system paths, show dialog:
   "This folder requires administrator privileges. Restart as admin?"
3. On confirm: `ElevationService.RestartAsAdmin(path)` starts new process with `runas` verb
4. New admin instance receives the path via command-line arg and starts scanning
5. On decline: skip inaccessible folders, scan what's accessible

## Navigation

- `NavigationHistory` stack tracks node zoom levels
- **Zoom in:** Push current → navigate to children → re-layout
- **Back:** Pop stack → re-layout to previous node
- Back button disabled when history is empty

## Data Flow

```
User menu action
    → MainViewModel.ScanCommand
    → DiskScanner.ScanAsync(path) — async
    → FileNode tree built
    → MainViewModel.CurrentRoot = tree root
    → TreemapControl.ItemsSource updated
    → TreemapLayout.Squarify() computes Rects
    → OnRender draws treemap
    → User hovers/clicks → ToolTip / NavigateTo
```

## Interaction Summary

| Action | Behavior |
|--------|----------|
| Select folder | FolderBrowserDialog → scan |
| Select drive | Drive list dialog → scan |
| Scan all drives | Scan each drive, aggregate view |
| Hover block | ToolTip: name, size, ratio |
| Click folder | Zoom to that folder as root |
| Back | Pop navigation history, re-layout |
| During scan | Progress bar + current path, cancel button |
