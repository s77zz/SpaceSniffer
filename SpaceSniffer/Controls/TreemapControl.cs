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

    private List<(FileNode Node, Rect Rect)> _layout = new();
    private FileNode? _hoveredNode;
    private readonly Dictionary<FileNode, Color> _colorCache = new();

    private const double MinNestSize = 30.0;

    private static readonly Random _rng = new();
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

        _layout.Clear();
        BuildNestedLayout(ItemsSource, bounds);

        foreach (var (node, rect) in _layout)
        {
            if (rect.Width < 1 || rect.Height < 1) continue;

            var color = GetNodeColor(node);
            var isHovered = node == _hoveredNode;
            var fillColor = isHovered ? Lighten(color, 0.3f) : color;

            dc.DrawRectangle(new SolidColorBrush(fillColor), null, rect);
            dc.DrawRectangle(null, new Pen(Brushes.Black, 0.5), rect);

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

    private void BuildNestedLayout(FileNode node, Rect bounds)
    {
        if (node.Children.Count == 0) return;

        var childLayout = TreemapLayout.Squarify(node.Children, bounds);
        foreach (var (child, childRect) in childLayout)
        {
            _layout.Add((child, childRect));

            if (child.Type == FileNodeType.Folder && child.Children.Count > 0
                && childRect.Width >= MinNestSize && childRect.Height >= MinNestSize)
            {
                BuildNestedLayout(child, childRect);
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
        for (int i = _layout.Count - 1; i >= 0; i--)
        {
            if (_layout[i].Rect.Contains(point))
                return _layout[i].Node;
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
            control._colorCache.Clear();
            control.InvalidateVisual();
        }
    }
}
