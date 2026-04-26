using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SpaceSniffer.Helpers;
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

    // Minimum rectangle size for showing nested children (pixels)
    // Below this, the rectangle is too small for the human eye to perceive sub-division
    private const double MinNestWidth = 40;
    private const double MinNestHeight = 32;

    // Padding between parent rect and its nested children
    private const double NestPadding = 2;

    private Dictionary<FileNode, Rect> _nodeRects = new();
    private FileNode? _hoveredNode;
    private readonly Dictionary<FileNode, Color> _colorCache = new();

    private static readonly Color[] FolderPalette =
    {
        Color.FromRgb(0xE8, 0x8D, 0x5C),
        Color.FromRgb(0xE6, 0xA8, 0x6A),
        Color.FromRgb(0xD4, 0x7B, 0x4A),
        Color.FromRgb(0xE0, 0x96, 0x5E),
        Color.FromRgb(0xC0, 0x7C, 0x4E),
    };

    private static readonly Color[] FilePalette =
    {
        Color.FromRgb(0x5C, 0x8D, 0xE8),
        Color.FromRgb(0x6A, 0xA8, 0xE6),
        Color.FromRgb(0x4A, 0x7B, 0xD4),
        Color.FromRgb(0x5E, 0x96, 0xE0),
        Color.FromRgb(0x4E, 0x7C, 0xC0),
    };

    protected override void OnRender(DrawingContext dc)
    {
        if (ItemsSource == null) return;

        var bounds = new Rect(0, 0, ActualWidth, ActualHeight);
        if (bounds.Width <= 0 || bounds.Height <= 0) return;

        if (ItemsSource.Children.Count == 0) return;

        // Recursively build layout for all visible levels
        _nodeRects = new Dictionary<FileNode, Rect>();
        BuildLayout(ItemsSource, bounds);

        // Draw all nodes (deeper levels drawn on top = correct nesting)
        foreach (var (node, rect) in _nodeRects)
        {
            if (rect.Width < 1 || rect.Height < 1) continue;

            var color = GetNodeColor(node);
            var isHovered = node == _hoveredNode;
            var fillColor = isHovered ? Lighten(color, 0.3f) : color;

            dc.DrawRectangle(new SolidColorBrush(fillColor), null, rect);

            // Darker border for folders, lighter for files
            var borderPen = node.Type == FileNodeType.Folder
                ? new Pen(Brushes.Black, 1.0)
                : new Pen(Brushes.Black, 0.5);
            dc.DrawRectangle(null, borderPen, rect);

            // Draw label if rectangle is large enough
            if (rect.Width > 40 && rect.Height > 20)
            {
                DrawNodeLabel(dc, node, rect);
            }
        }
    }

    private void BuildLayout(FileNode parent, Rect bounds)
    {
        if (parent.Children.Count == 0) return;

        var childLayout = TreemapLayout.Squarify(parent.Children, bounds);

        // Register all children at this level
        foreach (var (child, childRect) in childLayout)
        {
            _nodeRects[child] = childRect;
        }

        // Recurse into folder children that are large enough to show sub-division
        foreach (var (child, childRect) in childLayout)
        {
            if (child.Type == FileNodeType.Folder && child.Children.Count > 0
                && childRect.Width >= MinNestWidth && childRect.Height >= MinNestHeight)
            {
                // Leave small padding so parent border is visible
                var innerRect = new Rect(
                    childRect.X + NestPadding,
                    childRect.Y + NestPadding,
                    Math.Max(0, childRect.Width - NestPadding * 2),
                    Math.Max(0, childRect.Height - NestPadding * 2));

                BuildLayout(child, innerRect);
            }
        }
    }

    private static void DrawNodeLabel(DrawingContext dc, FileNode node, Rect rect)
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

        // Size label (only for leaf folders or when single-level shown)
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
        FileNode? best = null;
        double bestArea = double.MaxValue;
        foreach (var (node, rect) in _nodeRects)
        {
            if (rect.Contains(point))
            {
                double area = rect.Width * rect.Height;
                if (area < bestArea)
                {
                    bestArea = area;
                    best = node;
                }
            }
        }
        return best;
    }

    private Color GetNodeColor(FileNode node)
    {
        if (_colorCache.TryGetValue(node, out var cached))
            return cached;

        var palette = node.Type == FileNodeType.Folder ? FolderPalette : FilePalette;
        var color = palette[Math.Abs(node.FullPath.GetHashCode()) % palette.Length];
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

    private static string FormatSize(long bytes) => FormatHelper.FormatSize(bytes);

    private static void OnItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is TreemapControl control)
        {
            control._colorCache.Clear();
            control.InvalidateVisual();
        }
    }
}
