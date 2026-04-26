# SpaceSniffer Treemap Disk Analyzer — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a WPF disk space analyzer with interactive Squarified Treemap visualization and UAC elevation support.

**Architecture:** WPF (.NET 9, Windows) + CommunityToolkit.Mvvm. Custom `TreemapControl` renders via `DrawingContext`. `DiskScanner` builds a `FileNode` tree asynchronously. `MainViewModel` orchestrates scanning, navigation, and interaction.

**Tech Stack:** .NET 9.0-windows, WPF, CommunityToolkit.Mvvm (NuGet)

---

### Task 1: Add NuGet Package and Create Project Structure

**Files:**
- Modify: `SpaceSniffer/SpaceSniffer.csproj`

- [ ] **Step 1: Add CommunityToolkit.Mvvm NuGet package**

Run: `dotnet add SpaceSniffer/SpaceSniffer.csproj package CommunityToolkit.Mvvm`

Expected output: `PackageReference for 'CommunityToolkit.Mvvm' added`

- [ ] **Step 2: Build to confirm**

Run: `dotnet build`
Expected: Build succeeded

---

### Task 2: Create FileNode Model

**Files:**
- Create: `SpaceSniffer/Models/FileNode.cs`
- Create: `SpaceSniffer/Models/FileNodeType.cs`

- [ ] **Step 1: Create FileNodeType enum**

Create `SpaceSniffer/Models/FileNodeType.cs`:

```csharp
namespace SpaceSniffer.Models;

public enum FileNodeType
{
    Folder,
    File
}
```

- [ ] **Step 2: Create FileNode model**

Create `SpaceSniffer/Models/FileNode.cs`:

```csharp
using CommunityToolkit.Mvvm.ComponentModel;

namespace SpaceSniffer.Models;

public class FileNode : ObservableObject
{
    private string _name = "";
    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    private string _fullPath = "";
    public string FullPath
    {
        get => _fullPath;
        set => SetProperty(ref _fullPath, value);
    }

    private long _size;
    public long Size
    {
        get => _size;
        set => SetProperty(ref _size, value);
    }

    private FileNodeType _type;
    public FileNodeType Type
    {
        get => _type;
        set => SetProperty(ref _type, value);
    }

    private double _sizeRatio;
    public double SizeRatio
    {
        get => _sizeRatio;
        set => SetProperty(ref _sizeRatio, value);
    }

    public ObservableCollection<FileNode> Children { get; set; } = new();
}
```

- [ ] **Step 3: Build to confirm**

Run: `dotnet build`
Expected: Build succeeded

---

### Task 3: Implement Squarified Treemap Layout Algorithm

**Files:**
- Create: `SpaceSniffer/Controls/TreemapLayout.cs`

This implements the Bruls-Huizing-van Wijk squarified treemap algorithm. It takes a list of weighted items and a bounding rectangle, and returns positioned rectangles.

- [ ] **Step 1: Create TreemapLayout class with the squarify algorithm**

Create `SpaceSniffer/Controls/TreemapLayout.cs`:

```csharp
using System.Windows;
using SpaceSniffer.Models;

namespace SpaceSniffer.Controls;

public static class TreemapLayout
{
    public static Dictionary<FileNode, Rect> Squarify(IEnumerable<FileNode> items, Rect bounds)
    {
        var sorted = items
            .Where(n => n.Size > 0)
            .OrderByDescending(n => n.Size)
            .ToList();

        var result = new Dictionary<FileNode, Rect>();
        if (sorted.Count == 0) return result;

        var totalSize = sorted.Sum(n => n.Size);
        if (totalSize == 0) return result;

        SquarifyRecursive(sorted, bounds, totalSize, result);
        return result;
    }

    private static void SquarifyRecursive(
        List<FileNode> items, Rect bounds, double totalSize,
        Dictionary<FileNode, Rect> result)
    {
        if (items.Count == 0) return;
        if (items.Count == 1)
        {
            result[items[0]] = bounds;
            return;
        }

        var row = new List<FileNode>();
        var remaining = new List<FileNode>(items);
        double rowLength = bounds.Width > bounds.Height ? bounds.Width : bounds.Height;
        double rowSize = 0;

        while (remaining.Count > 0)
        {
            var candidate = remaining[0];
            var testRow = new List<FileNode>(row) { candidate };
            double testRowSize = rowSize + candidate.Size;

            if (row.Count > 0 && WorstAspectRatio(row, rowSize, rowLength, totalSize) <
                WorstAspectRatio(testRow, testRowSize, rowLength, totalSize))
                break;

            row.Add(candidate);
            rowSize += candidate.Size;
            remaining.RemoveAt(0);
        }

        // Layout the row
        double rowArea = (rowSize / totalSize) * (bounds.Width * bounds.Height);
        bool isHorizontal = bounds.Width >= bounds.Height;
        double rowThickness = rowArea / rowLength;

        double x = bounds.X;
        double y = bounds.Y;

        foreach (var item in row)
        {
            double itemArea = (item.Size / totalSize) * (bounds.Width * bounds.Height);
            double itemLength = itemArea / rowThickness;

            if (isHorizontal)
            {
                result[item] = new Rect(x, y, itemLength, rowThickness);
                x += itemLength;
            }
            else
            {
                result[item] = new Rect(x, y, rowThickness, itemLength);
                y += itemLength;
            }
        }

        // Remaining bounds
        Rect remainingBounds;
        if (isHorizontal)
        {
            remainingBounds = new Rect(bounds.X, y + rowThickness - bounds.Y > 0 ? y + rowThickness : bounds.Y + rowThickness,
                                       bounds.Width, bounds.Height - rowThickness);
            remainingBounds.Y = bounds.Y + rowThickness;
            remainingBounds.Height = bounds.Height - rowThickness;
        }
        else
        {
            remainingBounds = new Rect(bounds.X + rowThickness, bounds.Y,
                                       bounds.Width - rowThickness, bounds.Height);
        }

        if (remainingBounds.Width > 0 && remainingBounds.Height > 0 && remaining.Count > 0)
        {
            var remainingTotal = remaining.Sum(n => n.Size);
            SquarifyRecursive(remaining, remainingBounds, remainingTotal, result);
        }
    }

    private static double WorstAspectRatio(List<FileNode> row, double rowSize, double rowLength, double totalSize)
    {
        if (rowSize == 0 || totalSize == 0) return double.MaxValue;

        double rowArea = (rowSize / totalSize) * (rowLength * rowLength);
        double rowThickness = rowArea / rowLength;

        double worst = 0;
        foreach (var item in row)
        {
            double itemArea = (item.Size / totalSize) * (rowLength * rowLength);
            double itemLength = itemArea / rowThickness;

            double aspect = Math.Max(itemLength / rowThickness, rowThickness / itemLength);
            worst = Math.Max(worst, aspect);
        }
        return worst;
    }
}
```

- [ ] **Step 2: Build to confirm**

Run: `dotnet build`
Expected: Build succeeded

---

### Task 4: Implement SizeFormatConverter

**Files:**
- Create: `SpaceSniffer/Converters/SizeFormatConverter.cs`

- [ ] **Step 1: Create the converter**

Create `SpaceSniffer/Converters/SizeFormatConverter.cs`:

```csharp
using System.Globalization;
using System.Windows.Data;

namespace SpaceSniffer.Converters;

[ValueConversion(typeof(long), typeof(string))]
public class SizeFormatConverter : IValueConverter
{
    private static readonly string[] Units = { "B", "KB", "MB", "GB", "TB" };

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is long bytes && bytes >= 0)
        {
            double size = bytes;
            int unitIndex = 0;
            while (size >= 1024 && unitIndex < Units.Length - 1)
            {
                size /= 1024;
                unitIndex++;
            }
            return $"{size:0.##} {Units[unitIndex]}";
        }
        return "0 B";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
```

- [ ] **Step 2: Build to confirm**

Run: `dotnet build`
Expected: Build succeeded

---

### Task 5: Implement DiskScanner Service

**Files:**
- Create: `SpaceSniffer/Services/DiskScanner.cs`

- [ ] **Step 1: Create DiskScanner**

Create `SpaceSniffer/Services/DiskScanner.cs`:

```csharp
using SpaceSniffer.Models;

namespace SpaceSniffer.Services;

public class DiskScanner
{
    public async Task<FileNode> ScanAsync(string path, IProgress<(int percent, string currentPath)>? progress = null, CancellationToken ct = default)
    {
        var root = new FileNode
        {
            Name = Path.GetFileName(path) ?? path,
            FullPath = path,
            Type = FileNodeType.Folder
        };

        await Task.Run(() => ScanDirectory(root, progress, ct), ct);
        return root;
    }

    private void ScanDirectory(FileNode node, IProgress<(int, string)>? progress, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        try
        {
            var directoryInfo = new DirectoryInfo(node.FullPath);

            // Scan files
            foreach (var file in directoryInfo.EnumerateFiles())
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    var fileNode = new FileNode
                    {
                        Name = file.Name,
                        FullPath = file.FullName,
                        Size = file.Length,
                        Type = FileNodeType.File
                    };
                    node.Children.Add(fileNode);
                    node.Size += file.Length;
                }
                catch (UnauthorizedAccessException)
                {
                    // Skip inaccessible files
                }
            }

            // Scan subdirectories in parallel with limited concurrency
            var subDirs = new List<DirectoryInfo>();
            try
            {
                foreach (var dir in directoryInfo.EnumerateDirectories())
                {
                    ct.ThrowIfCancellationRequested();
                    subDirs.Add(dir);
                }
            }
            catch (UnauthorizedAccessException)
            {
                // Skip inaccessible directories at this level
            }

            var lockObj = new object();
            Parallel.ForEach(subDirs, new ParallelOptions
            {
                CancellationToken = ct,
                MaxDegreeOfParallelism = Environment.ProcessorCount
            }, dir =>
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    var dirNode = new FileNode
                    {
                        Name = dir.Name,
                        FullPath = dir.FullName,
                        Type = FileNodeType.Folder
                    };

                    ScanDirectory(dirNode, progress, ct);

                    lock (lockObj)
                    {
                        node.Children.Add(dirNode);
                        node.Size += dirNode.Size;
                    }

                    progress?.Report((0, dir.FullName));
                }
                catch (UnauthorizedAccessException)
                {
                    // Skip inaccessible subdirectories
                }
                catch (OperationCanceledException)
                {
                    // Cancellation handled at higher level
                }
            });

            // Calculate size ratios
            foreach (var child in node.Children)
            {
                child.SizeRatio = node.Size > 0 ? (double)child.Size / node.Size : 0;
            }
        }
        catch (UnauthorizedAccessException)
        {
            // Skip inaccessible directories
        }
    }
}
```

- [ ] **Step 2: Build to confirm**

Run: `dotnet build`
Expected: Build succeeded

---

### Task 6: Implement ElevationService

**Files:**
- Create: `SpaceSniffer/Services/ElevationService.cs`

- [ ] **Step 1: Create ElevationService**

Create `SpaceSniffer/Services/ElevationService.cs`:

```csharp
using System.Diagnostics;
using System.Security.Principal;

namespace SpaceSniffer.Services;

public static class ElevationService
{
    public static bool IsRunningAsAdmin()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    public static void RestartAsAdmin(string? path = null)
    {
        var args = string.IsNullOrEmpty(path) ? "" : $"--scan \"{path}\"";
        var process = new ProcessStartInfo
        {
            FileName = Environment.ProcessPath,
            Arguments = args,
            Verb = "runas",
            UseShellExecute = true
        };

        try
        {
            Process.Start(process);
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // User cancelled UAC dialog — do nothing
            return;
        }

        Environment.Exit(0);
    }
}
```

- [ ] **Step 2: Build to confirm**

Run: `dotnet build`
Expected: Build succeeded

---

### Task 7: Implement TreemapControl

**Files:**
- Create: `SpaceSniffer/Controls/TreemapControl.cs`

This is the custom WPF control that renders the treemap using `DrawingContext`.

- [ ] **Step 1: Create TreemapControl**

Create `SpaceSniffer/Controls/TreemapControl.cs`:

```csharp
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SpaceSniffer.Models;

namespace SpaceSniffer.Controls;

public class TreemapControl : FrameworkElement
{
    public static readonly DependencyProperty ItemsSourceProperty =
        DependencyProperty.Register(nameof(ItemsSource), typeof(FileNode), typeof(TreemapControl),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, OnItemsSourceChanged));

    public FileNode? ItemsSource
    {
        get => (FileNode?)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public static readonly DependencyProperty SelectedNodeProperty =
        DependencyProperty.Register(nameof(SelectedNode), typeof(FileNode), typeof(TreemapControl),
            new FrameworkPropertyMetadata(null));

    public FileNode? SelectedNode
    {
        get => (FileNode?)GetValue(SelectedNodeProperty);
        set => SetValue(SelectedNodeProperty, value);
    }

    public event EventHandler<FileNode>? NodeSelected;

    private Dictionary<FileNode, Rect> _nodeRects = new();
    private FileNode? _hoveredNode;
    private readonly Dictionary<FileNode, Color> _colorCache = new();

    private static readonly Random _rng = new();
    private static readonly Color[] FolderPalette =
    {
        Color.FromRgb(0xE8, 0x8D, 0x5C), // warm orange
        Color.FromRgb(0xE6, 0xA8, 0x6A), // light orange
        Color.FromRgb(0xD4, 0x7B, 0x4A), // deep orange
        Color.FromRgb(0xE0, 0x96, 0x5E), // golden orange
        Color.FromRgb(0xC0, 0x7C, 0x4E), // brown-orange
    };

    private static readonly Color[] FilePalette =
    {
        Color.FromRgb(0x5C, 0x8D, 0xE8), // blue
        Color.FromRgb(0x6A, 0xA8, 0xE6), // light blue
        Color.FromRgb(0x4A, 0x7B, 0xD4), // deep blue
        Color.FromRgb(0x5E, 0x96, 0xE0), // medium blue
        Color.FromRgb(0x4E, 0x7C, 0xC0), // darker blue
    };

    protected override void OnRender(DrawingContext dc)
    {
        if (ItemsSource == null) return;

        var bounds = new Rect(0, 0, ActualWidth, ActualHeight);
        if (bounds.Width <= 0 || bounds.Height <= 0) return;

        // Layout: top level -> ItemsSource's children
        var layoutItems = ItemsSource.Children.ToList();
        if (layoutItems.Count == 0) return;

        _nodeRects = TreemapLayout.Squarify(layoutItems, bounds);
        _colorCache.Clear();

        foreach (var (node, rect) in _nodeRects)
        {
            if (rect.Width < 1 || rect.Height < 1) continue;

            var color = GetNodeColor(node);
            var isHovered = node == _hoveredNode;
            var fillColor = isHovered ? Lighten(color, 0.3f) : color;

            dc.DrawRectangle(new SolidColorBrush(fillColor), null, rect);

            // Border
            dc.DrawRectangle(null, new Pen(Brushes.Black, 0.5), rect);

            // Draw label if rectangle is large enough
            if (rect.Width > 40 && rect.Height > 20)
            {
                var formattedText = new FormattedText(
                    node.Name,
                    System.Globalization.CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    new Typeface("Segoe UI"),
                    11,
                    Brushes.White,
                    1.0);

                // Trim text if too wide
                while (formattedText.Width > rect.Width - 6 && formattedText.Text.Length > 3)
                {
                    formattedText = new FormattedText(
                        formattedText.Text[..^4] + "...",
                        System.Globalization.CultureInfo.CurrentCulture,
                        FlowDirection.LeftToRight,
                        new Typeface("Segoe UI"),
                        11,
                        Brushes.White,
                        1.0);
                }

                dc.DrawText(formattedText, new Point(rect.X + 3, rect.Y + 3));

                // Size label
                var sizeText = FormatSize(node.Size);
                var sizeFormatted = new FormattedText(
                    sizeText,
                    System.Globalization.CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    new Typeface("Segoe UI"),
                    10,
                    Brushes.LightGray,
                    1.0);
                dc.DrawText(sizeFormatted, new Point(rect.X + 3, rect.Y + 16));
            }
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        var pos = e.GetPosition(this);
        var hitNode = HitTest(pos);

        if (hitNode != _hoveredNode)
        {
            _hoveredNode = hitNode;
            InvalidateVisual();

            if (hitNode != null)
            {
                var sizeFormatted = FormatSize(hitNode.Size);
                var ratio = hitNode.SizeRatio > 0 ? $"{hitNode.SizeRatio * 100:F1}%" : "";
                ToolTip = $"{hitNode.Name}\n{sizeFormatted}\n{ratio}";
            }
            else
            {
                ToolTip = null;
            }
        }
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        var pos = e.GetPosition(this);
        var hitNode = HitTest(pos);

        if (hitNode != null)
        {
            SelectedNode = hitNode;
            NodeSelected?.Invoke(this, hitNode);
        }
    }

    private FileNode? HitTest(Point point)
    {
        foreach (var (node, rect) in _nodeRects)
        {
            if (rect.Contains(point))
                return node;
        }
        return null;
    }

    private Color GetNodeColor(FileNode node)
    {
        if (_colorCache.TryGetValue(node, out var cached))
            return cached;

        var palette = node.Type == FileNodeType.Folder ? FolderPalette : FilePalette;
        var color = palette[_rng.Next(palette.Length)];
        _colorCache[node] = color;
        return color;
    }

    private static Color Lighten(Color color, float factor)
    {
        return Color.FromRgb(
            (byte)Math.Min(255, color.R + (255 - color.R) * factor),
            (byte)Math.Min(255, color.G + (255 - color.G) * factor),
            (byte)Math.Min(255, color.B + (255 - color.B) * factor));
    }

    private static string FormatSize(long bytes)
    {
        string[] units = { "B", "KB", "MB", "GB", "TB" };
        double size = bytes;
        int unitIndex = 0;
        while (size >= 1024 && unitIndex < units.Length - 1)
        {
            size /= 1024;
            unitIndex++;
        }
        return $"{size:0.##} {units[unitIndex]}";
    }

    private static void OnItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is TreemapControl control)
        {
            control.InvalidateVisual();
        }
    }
}
```

- [ ] **Step 2: Build to confirm**

Run: `dotnet build`
Expected: Build succeeded

---

### Task 8: Implement MainViewModel

**Files:**
- Create: `SpaceSniffer/ViewModels/MainViewModel.cs`
- Create: `SpaceSniffer/ViewModels/RelayCommand.cs` (not needed — comes from CommunityToolkit.Mvvm)

- [ ] **Step 1: Create MainViewModel**

Create `SpaceSniffer/ViewModels/MainViewModel.cs`:

```csharp
using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using SpaceSniffer.Models;
using SpaceSniffer.Services;

namespace SpaceSniffer.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly DiskScanner _scanner = new();
    private CancellationTokenSource? _cts;

    [ObservableProperty]
    private FileNode? _currentRoot;

    [ObservableProperty]
    private bool _isScanning;

    [ObservableProperty]
    private string _statusText = "Ready";

    private readonly Stack<FileNode> _navigationHistory = new();

    public bool CanGoBack => _navigationHistory.Count > 0;

    public string WindowTitle => CurrentPath != null
        ? $"SpaceSniffer - {CurrentPath}"
        : "SpaceSniffer";

    [ObservableProperty]
    private string _currentPath = "";

    // Scan progress
    [ObservableProperty]
    private int _scanProgressPercent;

    [ObservableProperty]
    private string _scanProgressText = "";

    [RelayCommand]
    private async Task SelectFolder()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "选择要分析的文件夹"
        };

        if (dialog.ShowDialog() == true)
        {
            await StartScan(dialog.FolderName);
        }
    }

    [RelayCommand]
    private async Task SelectDrive()
    {
        var drives = DriveInfo.GetDrives()
            .Where(d => d.IsReady)
            .Select(d => d.Name.TrimEnd('\\'))
            .ToList();

        if (drives.Count == 0) return;

        // Simple selection via a custom dialog approach — use a string list
        var drive = drives.Count == 1
            ? drives[0]
            : PickDriveFromList(drives);

        if (drive != null)
        {
            await StartScan(drive);
        }
    }

    [RelayCommand]
    private async Task ScanAllDrives()
    {
        var drives = DriveInfo.GetDrives()
            .Where(d => d.IsReady)
            .Select(d => d.Name.TrimEnd('\\'))
            .ToList();

        if (drives.Count == 0) return;

        if (drives.Count == 1)
        {
            await StartScan(drives[0]);
            return;
        }

        // For multiple drives, show them all at the top level
        var root = new FileNode
        {
            Name = "All Drives",
            FullPath = "All Drives",
            Type = FileNodeType.Folder
        };

        IsScanning = true;
        CurrentPath = "All Drives";
        CurrentRoot = root;
        _navigationHistory.Clear();
        OnPropertyChanged(nameof(CanGoBack));

        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        try
        {
            var allDrives = drives.ToList();
            for (int i = 0; i < allDrives.Count; i++)
            {
                token.ThrowIfCancellationRequested();
                StatusText = $"Scanning {allDrives[i]}...";
                var driveNode = await _scanner.ScanAsync(allDrives[i], null, token);
                driveNode.SizeRatio = 1.0 / allDrives.Count;
                root.Children.Add(driveNode);
                root.Size += driveNode.Size;
                ScanProgressPercent = (i + 1) * 100 / allDrives.Count;
            }

            // Recalculate ratios after all drives scanned
            foreach (var child in root.Children)
            {
                child.SizeRatio = root.Size > 0 ? (double)child.Size / root.Size : 0;
            }

            StatusText = $"Scan complete — {FormatSize(root.Size)} total";
        }
        catch (OperationCanceledException)
        {
            StatusText = "Scan cancelled";
        }
        finally
        {
            IsScanning = false;
        }
    }

    [RelayCommand]
    private void NavigateTo(FileNode? node)
    {
        if (node == null || CurrentRoot == null) return;

        if (node.Type == FileNodeType.Folder && node.Children.Count > 0)
        {
            _navigationHistory.Push(CurrentRoot);
            CurrentRoot = node;
            OnPropertyChanged(nameof(CanGoBack));
        }
    }

    [RelayCommand]
    private void GoBack()
    {
        if (_navigationHistory.Count > 0)
        {
            CurrentRoot = _navigationHistory.Pop();
            OnPropertyChanged(nameof(CanGoBack));
        }
    }

    [RelayCommand]
    private void CancelScan()
    {
        _cts?.Cancel();
    }

    private async Task StartScan(string path)
    {
        if (string.IsNullOrEmpty(path)) return;

        // Check if admin is needed for protected paths
        if (!ElevationService.IsRunningAsAdmin())
        {
            try
            {
                var di = new DirectoryInfo(path);
                di.EnumerateFiles().Take(1).ToList(); // test access
            }
            catch (UnauthorizedAccessException)
            {
                var result = MessageBox.Show(
                    $"需要管理员权限来访问 {path}\n是否以管理员身份重启？",
                    "权限不足",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    ElevationService.RestartAsAdmin(path);
                }
                else
                {
                    // Continue — will skip inaccessible subfolders
                }
            }
        }

        _navigationHistory.Clear();
        CurrentPath = path;
        OnPropertyChanged(nameof(CanGoBack));
        OnPropertyChanged(nameof(WindowTitle));

        _cts = new CancellationTokenSource();
        var token = _cts.Token;
        IsScanning = true;

        try
        {
            var progress = new Progress<(int percent, string currentPath)>(p =>
            {
                ScanProgressPercent = p.percent;
                ScanProgressText = $"Scanning: {p.currentPath}";
                StatusText = $"Scanning: {p.currentPath}";
            });

            var result = await _scanner.ScanAsync(path, progress, token);
            CurrentRoot = result;
            StatusText = $"Scan complete — {FormatSize(result.Size)} in {result.Children.Count} items";
        }
        catch (OperationCanceledException)
        {
            StatusText = "Scan cancelled";
        }
        finally
        {
            IsScanning = false;
        }
    }

    private static string? PickDriveFromList(List<string> drives)
    {
        // Use a simple approach: build a comma-separated prompt
        // The view will handle this via a proper dialog
        return drives[0]; // fallback — MainWindow provides the real picker
    }

    private static string FormatSize(long bytes)
    {
        string[] units = { "B", "KB", "MB", "GB", "TB" };
        double size = bytes;
        int unitIndex = 0;
        while (size >= 1024 && unitIndex < units.Length - 1)
        {
            size /= 1024;
            unitIndex++;
        }
        return $"{size:0.##} {units[unitIndex]}";
    }
}
```

- [ ] **Step 2: Build to confirm**

Run: `dotnet build`
Expected: Build succeeded

---

### Task 9: Build MainWindow XAML and Code-Behind

**Files:**
- Modify: `SpaceSniffer/MainWindow.xaml` (full rewrite)
- Modify: `SpaceSniffer/MainWindow.xaml.cs` (full rewrite)

- [ ] **Step 1: Rewrite MainWindow.xaml**

```xml
<Window x:Class="SpaceSniffer.MainWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:controls="clr-namespace:SpaceSniffer.Controls"
        xmlns:vm="clr-namespace:SpaceSniffer.ViewModels"
        mc:Ignorable="d"
        xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
        xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
        d:DataContext="{d:DesignInstance vm:MainViewModel}"
        Title="{Binding WindowTitle}" Height="700" Width="1000"
        WindowState="Maximized">
    <Grid>
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="*"/>
            <RowDefinition Height="Auto"/>
        </Grid.RowDefinitions>

        <!-- Menu Bar -->
        <Menu Grid.Row="0">
            <MenuItem Header="_File">
                <MenuItem Header="Select _Folder..." Command="{Binding SelectFolderCommand}"/>
                <MenuItem Header="Select _Drive..." Command="{Binding SelectDriveCommand}"/>
                <MenuItem Header="Scan _All Drives" Command="{Binding ScanAllDrivesCommand}"/>
                <Separator/>
                <MenuItem Header="E_xit" Click="OnExitClick"/>
            </MenuItem>
            <MenuItem Header="_Navigation" IsEnabled="{Binding CanGoBack}">
                <MenuItem Header="_Back" Command="{Binding GoBackCommand}" InputGestureText="Alt+Left"/>
            </MenuItem>
        </Menu>

        <!-- Toolbar -->
        <ToolBar Grid.Row="0" Margin="0,24,0,0">
            <Button Content="← Back" Command="{Binding GoBackCommand}"
                    IsEnabled="{Binding CanGoBack}" ToolTip="Go back (Alt+Left)"/>
            <Separator/>
            <Button Content="Select Folder" Command="{Binding SelectFolderCommand}"/>
            <Button Content="Select Drive" Command="{Binding SelectDriveCommand}"/>
            <Button Content="All Drives" Command="{Binding ScanAllDrivesCommand}"/>
            <Separator/>
            <Button Content="Cancel" Command="{Binding CancelScanCommand}"
                    Visibility="{Binding IsScanning, Converter={StaticResource BoolToVisibilityConverter}}"/>
        </ToolBar>

        <!-- Treemap Control -->
        <controls:TreemapControl
            Grid.Row="1"
            x:Name="Treemap"
            ItemsSource="{Binding CurrentRoot}"
            Margin="4"/>

        <!-- Progress bar -->
        <ProgressBar Grid.Row="1"
                     Height="4"
                     VerticalAlignment="Top"
                     IsIndeterminate="{Binding IsScanning}"
                     Visibility="{Binding IsScanning, Converter={StaticResource BoolToVisibilityConverter}}"/>

        <!-- Status Bar -->
        <StatusBar Grid.Row="2">
            <StatusBarItem>
                <TextBlock Text="{Binding StatusText}"/>
            </StatusBarItem>
            <StatusBarItem HorizontalAlignment="Right">
                <TextBlock Text="{Binding ScanProgressText}"/>
            </StatusBarItem>
        </StatusBar>
    </Grid>
</Window>
```

- [ ] **Step 2: Rewrite MainWindow.xaml.cs**

```csharp
using System.Windows;
using SpaceSniffer.ViewModels;

namespace SpaceSniffer;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    public MainWindow()
    {
        InitializeComponent();

        _viewModel = new MainViewModel();
        DataContext = _viewModel;

        Treemap.NodeSelected += (_, node) =>
        {
            _viewModel.NavigateToCommand.Execute(node);
        };

        // Handle command-line --scan argument (elevated restart)
        var args = Environment.GetCommandLineArgs();
        if (args.Length > 1 && args[1] == "--scan" && args.Length > 2)
        {
            Loaded += async (_, _) =>
            {
                await _viewModel.SelectFolderCommand.ExecuteAsync(null);
                // Actually we need a direct scan method exposed
                var path = args[2];
                var method = typeof(MainViewModel).GetMethod("StartScan",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (method != null)
                {
                    var task = (Task?)method.Invoke(_viewModel, new object[] { path });
                    if (task != null) await task;
                }
            };
        }
    }

    private void OnExitClick(object sender, RoutedEventArgs e)
    {
        Application.Current.Shutdown();
    }
}
```

Wait — using reflection is fragile. Let me refactor the approach: make `StartScan` internal or use a public method.

- [ ] **Step 3: Add a public method to MainViewModel for command-line scan**

Add to `MainViewModel.cs`:

```csharp
// Public entry point for command-line elevated restart
public Task ScanPathAsync(string path) => StartScan(path);
```

Then replace the reflection code in `MainWindow.xaml.cs`:

```csharp
Loaded += async (_, _) =>
{
    await _viewModel.ScanPathAsync(args[2]);
};
```

- [ ] **Step 4: Build to confirm**

Run: `dotnet build`
Expected: Build succeeded

---

### Task 10: Wire Up App.xaml with Resources and Update AssemblyInfo

**Files:**
- Modify: `SpaceSniffer/App.xaml`
- Modify: `SpaceSniffer/App.xaml.cs`

- [ ] **Step 1: Add BoolToVisibilityConverter to App.xaml resources**

```xml
<Application x:Class="SpaceSniffer.App"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:local="clr-namespace:SpaceSniffer.Converters"
             StartupUri="MainWindow.xaml">
    <Application.Resources>
        <local:SizeFormatConverter x:Key="SizeFormatConverter"/>
        <BooleanToVisibilityConverter x:Key="BoolToVisibilityConverter"/>
    </Application.Resources>
</Application>
```

Note: `BooleanToVisibilityConverter` is a built-in WPF converter, so no custom converter needed for that.

- [ ] **Step 2: Build and run**

Run: `dotnet run --project SpaceSniffer/SpaceSniffer.csproj`
Expected: Application window opens with menu bar, empty treemap area, and status bar.

---

### Task 11: Implement Drive Selection Dialog

**Files:**
- Create: `SpaceSniffer/Views/DriveSelectDialog.xaml`
- Create: `SpaceSniffer/Views/DriveSelectDialog.xaml.cs`

The Select Drive command currently has a fallback placeholder. Replace it with a proper dialog.

- [ ] **Step 1: Create DriveSelectDialog XAML**

Create directory `SpaceSniffer/Views/` first.

Create `SpaceSniffer/Views/DriveSelectDialog.xaml`:

```xml
<Window x:Class="SpaceSniffer.Views.DriveSelectDialog"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="Select Drive" Height="300" Width="400"
        WindowStartupLocation="CenterOwner"
        WindowStyle="ToolWindow"
        ResizeMode="NoResize"
        ShowInTaskbar="False">
    <Grid Margin="12">
        <Grid.RowDefinitions>
            <RowDefinition Height="*"/>
            <RowDefinition Height="Auto"/>
        </Grid.RowDefinitions>

        <ListBox x:Name="DriveListBox"
                 Grid.Row="0"
                 DisplayMemberPath="Name"
                 SelectionMode="Single"
                 MouseDoubleClick="OnDriveDoubleClick"/>

        <StackPanel Grid.Row="1" Orientation="Horizontal" HorizontalAlignment="Right" Margin="0,8,0,0">
            <Button Content="OK" Width="75" Margin="0,0,8,0" Click="OnOkClick" IsDefault="True"/>
            <Button Content="Cancel" Width="75" Click="OnCancelClick" IsCancel="True"/>
        </StackPanel>
    </Grid>
</Window>
```

- [ ] **Step 2: Create DriveSelectDialog code-behind**

Create `SpaceSniffer/Views/DriveSelectDialog.xaml.cs`:

```csharp
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;

namespace SpaceSniffer.Views;

public partial class DriveSelectDialog : Window
{
    public string? SelectedDrive { get; private set; }

    public DriveSelectDialog()
    {
        InitializeComponent();

        var drives = DriveInfo.GetDrives()
            .Where(d => d.IsReady)
            .Select(d => new DriveItem
            {
                Name = d.Name.TrimEnd('\\'),
                Label = $"{d.Name.TrimEnd('\\')}  ({FormatSize(d.TotalSize)} / {FormatSize(d.AvailableFreeSpace)} free)"
            })
            .ToList();

        DriveListBox.ItemsSource = drives;
    }

    private void OnDriveDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DriveListBox.SelectedItem is DriveItem item)
        {
            SelectedDrive = item.Name;
            DialogResult = true;
            Close();
        }
    }

    private void OnOkClick(object sender, RoutedEventArgs e)
    {
        if (DriveListBox.SelectedItem is DriveItem item)
        {
            SelectedDrive = item.Name;
            DialogResult = true;
        }
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private static string FormatSize(long bytes)
    {
        string[] units = { "B", "KB", "MB", "GB", "TB" };
        double size = bytes;
        int unitIndex = 0;
        while (size >= 1024 && unitIndex < units.Length - 1)
        {
            size /= 1024;
            unitIndex++;
        }
        return $"{size:0.##} {units[unitIndex]}";
    }

    public class DriveItem
    {
        public string Name { get; set; } = "";
        public string Label { get; set; } = "";
    }
}
```

- [ ] **Step 3: Update MainViewModel.SelectDrive to use the dialog**

Replace the placeholder `PickDriveFromList` call in `MainViewModel.cs` with:

```csharp
[RelayCommand]
private async Task SelectDrive()
{
    var dialog = new Views.DriveSelectDialog();
    if (dialog.ShowDialog() == true && dialog.SelectedDrive != null)
    {
        await StartScan(dialog.SelectedDrive);
    }
}
```

Remove the `PickDriveFromList` method.

- [ ] **Step 4: Build to confirm**

Run: `dotnet build`
Expected: Build succeeded

---

### Task 12: Final Integration and Test

- [ ] **Step 1: Full build**

Run: `dotnet build`
Expected: Build succeeded, 0 warnings

- [ ] **Step 2: Run the application**

Run: `dotnet run --project SpaceSniffer/SpaceSniffer.csproj`

Manually verify:
1. Menu shows File > Select Folder / Select Drive / Scan All Drives / Exit
2. Navigation > Back button available after zooming
3. Toolbar buttons for quick access
4. Status bar shows scan progress
5. Treemap renders after selecting a folder
6. Hover shows tooltip with name/size
7. Click zooms into a folder
8. Back button returns to previous view
9. Cancel button works during scan
