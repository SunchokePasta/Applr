using System.Windows;
using JobFinder.Desktop.ViewModels;

namespace JobFinder.Desktop;

public partial class MainWindow : Window
{
    public MainWindow(MainWindowViewModel viewModel)
    {
        InitializeComponent();

        DataContext = viewModel;
    }
}