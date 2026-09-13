using System.IO;
using System.Text.Json;
using System.Windows;
using JobFinder.Desktop.ViewModels;
using Microsoft.Web.WebView2.Core;

namespace JobFinder.Desktop;

public partial class MainWindow : Window
{
    private const string WebHostName = "jobfinder.local";
    private static readonly JsonSerializerOptions WebJsonOptions =
        new(JsonSerializerDefaults.Web);
    private readonly MainWindowViewModel _viewModel;

    public MainWindow(MainWindowViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        Loaded += MainWindow_Loaded;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            await JobFinderWebView.EnsureCoreWebView2Async();

            var webFilesPath = Path.Combine(AppContext.BaseDirectory, "Web");
            JobFinderWebView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                WebHostName,
                webFilesPath,
                CoreWebView2HostResourceAccessKind.Allow);
            JobFinderWebView.CoreWebView2.WebMessageReceived += WebMessageReceived;
            JobFinderWebView.CoreWebView2.Navigate($"https://{WebHostName}/index.html");
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"The JobFinder interface could not be loaded. {ex.Message}",
                "JobFinder",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private async void WebMessageReceived(
        object? sender,
        CoreWebView2WebMessageReceivedEventArgs e)
    {
        try
        {
            using var message = JsonDocument.Parse(e.WebMessageAsJson);
            if (!message.RootElement.TryGetProperty("type", out var type) ||
                type.GetString() != "loadJobs")
            {
                return;
            }

            var jobs = await _viewModel.LoadJobsAsync();
            SendMessageToWebUi(new { type = "jobsLoaded", jobs });
        }
        catch (Exception)
        {
            SendMessageToWebUi(new
            {
                type = "jobsFailed",
                message = "Jobs could not be loaded. Please try again."
            });
        }
    }

    private void SendMessageToWebUi(object message)
    {
        JobFinderWebView.CoreWebView2?.PostWebMessageAsJson(
            JsonSerializer.Serialize(message, WebJsonOptions));
    }
}
