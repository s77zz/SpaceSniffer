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
            _viewModel.StatusText = node.FullPath;
            _viewModel.NavigateToCommand.Execute(node);
        };

        var args = Environment.GetCommandLineArgs();
        if (args.Length > 1 && args[1] == "--scan" && args.Length > 2)
        {
            Loaded += async (_, _) =>
            {
                await _viewModel.ScanPathAsync(args[2]);
            };
        }
    }

    private void OnExitClick(object sender, RoutedEventArgs e)
    {
        Application.Current.Shutdown();
    }
}
