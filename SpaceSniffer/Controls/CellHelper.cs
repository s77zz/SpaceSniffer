using System.Windows.Media;
using SpaceSniffer.Models;

namespace SpaceSniffer.Controls;

internal static class CellHelper
{
    internal static Color Lighten(Color color, float factor)
    {
        return Color.FromRgb(
            (byte)Math.Min(255, color.R + (255 - color.R) * factor),
            (byte)Math.Min(255, color.G + (255 - color.G) * factor),
            (byte)Math.Min(255, color.B + (255 - color.B) * factor));
    }

    internal static string FormatSize(long bytes)
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

    internal static string BuildToolTip(FileNode node)
    {
        var text = $"{node.Name}\n{CellHelper.FormatSize(node.Size)}";
        if (node.SizeRatio > 0)
            text += $"  ({node.SizeRatio * 100:F1}%)";
        return text;
    }
}
