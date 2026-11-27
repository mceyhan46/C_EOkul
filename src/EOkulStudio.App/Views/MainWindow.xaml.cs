using EOkulStudio.Desktop.ViewModels;
using Wpf.Ui.Appearance;

namespace EOkulStudio.Desktop.Views;

public partial class MainWindow : Wpf.Ui.Controls.FluentWindow
{
    public MainWindow(MainViewModel viewModel)
    {
        DataContext = viewModel;
        // Windows açık/koyu tema değişikliğini izler.
        SystemThemeWatcher.Watch(this);
        InitializeComponent();
    }
}
