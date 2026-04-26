namespace SpaceSniffer.Helpers;

public static class FormatHelper
{
    private static readonly string[] Units = { "B", "KB", "MB", "GB", "TB" };

    public static string FormatSize(long bytes)
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
}
