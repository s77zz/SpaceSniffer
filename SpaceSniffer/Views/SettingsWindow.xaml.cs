using System.Windows;

namespace SpaceSniffer.Views;

public partial class SettingsWindow : Window
{
    public int SelectedMaxDepth => (int)DepthNumberBox.Value;

    public SettingsWindow()
    {
        InitializeComponent();
    }

    public void SetInitialDepth(int depth)
    {
        DepthNumberBox.Value = depth;
    }

    public void SetMaxAvailableDepth(int depth)
    {
        DepthNumberBox.Maximum = depth;
    }

    private void OnOkClick(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
