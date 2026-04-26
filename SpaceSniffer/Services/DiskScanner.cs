using System.IO;
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
