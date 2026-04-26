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
        double totalArea = bounds.Width * bounds.Height;

        while (remaining.Count > 0)
        {
            var candidate = remaining[0];
            var testRow = new List<FileNode>(row) { candidate };
            double testRowSize = rowSize + candidate.Size;

            if (row.Count > 0 && WorstAspectRatio(row, rowSize, rowLength, totalSize, totalArea) <
                WorstAspectRatio(testRow, testRowSize, rowLength, totalSize, totalArea))
                break;

            row.Add(candidate);
            rowSize += candidate.Size;
            remaining.RemoveAt(0);
        }

        // Layout the row
        double rowArea = (rowSize / totalSize) * totalArea;
        bool isHorizontal = bounds.Width >= bounds.Height;
        double rowThickness = rowArea / rowLength;

        double x = bounds.X;
        double y = bounds.Y;

        foreach (var item in row)
        {
            double itemArea = (item.Size / totalSize) * totalArea;
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

        // Remaining bounds — clamp to 0 to avoid floating-point precision issues
        Rect remainingBounds;
        if (isHorizontal)
        {
            double remainingHeight = Math.Max(0, bounds.Height - rowThickness);
            remainingBounds = new Rect(bounds.X, bounds.Y + rowThickness, bounds.Width, remainingHeight);
        }
        else
        {
            double remainingWidth = Math.Max(0, bounds.Width - rowThickness);
            remainingBounds = new Rect(bounds.X + rowThickness, bounds.Y, remainingWidth, bounds.Height);
        }

        if (remainingBounds.Width > 0 && remainingBounds.Height > 0 && remaining.Count > 0)
        {
            var remainingTotal = remaining.Sum(n => n.Size);
            SquarifyRecursive(remaining, remainingBounds, remainingTotal, result);
        }
    }

    private static double WorstAspectRatio(List<FileNode> row, double rowSize, double rowLength, double totalSize, double totalArea)
    {
        if (rowSize == 0 || totalSize == 0) return double.MaxValue;

        double rowArea = (rowSize / totalSize) * totalArea;
        double rowThickness = rowArea / rowLength;

        double worst = 0;
        foreach (var item in row)
        {
            double itemArea = (item.Size / totalSize) * totalArea;
            double itemLength = itemArea / rowThickness;

            double aspect = Math.Max(itemLength / rowThickness, rowThickness / itemLength);
            worst = Math.Max(worst, aspect);
        }
        return worst;
    }
}
