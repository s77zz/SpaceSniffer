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
            _viewModel.NavigateToCommand.Execute(node);
        };

        // Handle command-line --scan argument (elevated restart)
        var args = Environment.GetCommandLineArgs();
        if (args.Length > 1 && args[1] == "--scan" && args.Length > 2)
        {
            var path = args[2];
            Loaded += async (_, _) =>
            {
                await _viewModel.ScanPathAsync(path);
            };
        }
    }

    private void OnExitClick(object sender, RoutedEventArgs e)
    {
        Application.Current.Shutdown();
    }
}
