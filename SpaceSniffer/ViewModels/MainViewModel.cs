using System.Collections.ObjectModel;
using System.IO;
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
    private readonly System.Diagnostics.Stopwatch _scanStopwatch = new();
    private CancellationTokenSource? _cts;

    [ObservableProperty]
    private FileNode? _currentRoot;

    [ObservableProperty]
    private bool _isScanning;

    [ObservableProperty]
    private string _statusText = "Ready";

    private readonly Stack<FileNode> _navigationHistory = new();

    public bool CanGoBack => _navigationHistory.Count > 0;

    public string WindowTitle => !string.IsNullOrEmpty(CurrentPath)
        ? $"SpaceSniffer - {CurrentPath}"
        : "SpaceSniffer";

    [ObservableProperty]
    private string _currentPath = "";

    [ObservableProperty]
    private int _scanProgressPercent;

    [ObservableProperty]
    private string _scanProgressText = "";

    public MainViewModel()
    {
        OnPropertyChanged(nameof(WindowTitle));
    }

    [RelayCommand]
    private async Task SelectFolder()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Select a folder to analyze"
        };

        if (dialog.ShowDialog() == true)
        {
            await StartScan(dialog.FolderName);
        }
    }

    [RelayCommand]
    private async Task SelectDrive()
    {
        var dialog = new Views.DriveSelectDialog();
        if (dialog.ShowDialog() == true && dialog.SelectedDrive != null)
        {
            await StartScan(dialog.SelectedDrive);
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
        _scanStopwatch.Restart();

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

            foreach (var child in root.Children)
            {
                child.SizeRatio = root.Size > 0 ? (double)child.Size / root.Size : 0;
            }

            _scanStopwatch.Stop();
            StatusText = $"Scan complete — {FormatSize(root.Size)} total ({FormatDuration(_scanStopwatch.Elapsed)})";
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

    public async Task ScanPathAsync(string path)
    {
        if (!string.IsNullOrEmpty(path))
            await StartScan(path);
    }

    private async Task StartScan(string path)
    {
        if (string.IsNullOrEmpty(path)) return;

        if (!ElevationService.IsRunningAsAdmin())
        {
            try
            {
                var di = new DirectoryInfo(path);
                di.EnumerateFiles().Take(1).ToList();
            }
            catch (UnauthorizedAccessException)
            {
                var result = MessageBox.Show(
                    $"Administrator privileges required to access {path}\nRestart as administrator?",
                    "Insufficient Permissions",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    ElevationService.RestartAsAdmin(path);
                }
            }
        }

        _navigationHistory.Clear();
        CurrentPath = path;
        OnPropertyChanged(nameof(CanGoBack));
        OnPropertyChanged(nameof(WindowTitle));

        _cts = new CancellationTokenSource();
        var token = _cts.Token;
        _scanStopwatch.Restart();
        IsScanning = true;

        try
        {
            var progress = new Progress<(int percent, string currentPath)>(p =>
            {
                ScanProgressPercent = p.percent;
                ScanProgressText = $"Scanning: {p.currentPath}";
                StatusText = $"Scanning: {p.currentPath}";
            });

            var throttled = new ThrottledProgress<(int percent, string currentPath)>(progress, 200);
            var result = await _scanner.ScanAsync(path, throttled, token);
            _scanStopwatch.Stop();
            CurrentRoot = result;
            var itemCount = result.Children.Count;
            StatusText = $"Scan complete — {FormatSize(result.Size)} in {itemCount} items ({FormatDuration(_scanStopwatch.Elapsed)})";
            ScanProgressText = "";
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

    private static string FormatDuration(TimeSpan duration)
    {
        if (duration.TotalSeconds < 1)
            return $"{duration.Milliseconds}ms";
        if (duration.TotalMinutes < 1)
            return $"{duration.TotalSeconds:F1}s";
        return $"{(int)duration.TotalMinutes}m {duration.Seconds}s";
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
