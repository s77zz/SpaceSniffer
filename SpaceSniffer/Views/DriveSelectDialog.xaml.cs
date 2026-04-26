using System.IO;
using System.Windows;
using System.Windows.Input;
using SpaceSniffer.Helpers;

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

    private static string FormatSize(long bytes) => FormatHelper.FormatSize(bytes);

    public class DriveItem
    {
        public string Name { get; set; } = "";
        public string Label { get; set; } = "";
    }
}
