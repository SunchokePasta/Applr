using System.IO;
using System.Text.Json;
using System.Windows;
using Applr.Desktop.ViewModels;
using Microsoft.Web.WebView2.Core;

namespace Applr.Desktop;

public partial class MainWindow : Window
{
    private const string WebHostName = "applr.local";
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
            await ApplrWebView.EnsureCoreWebView2Async();

            var webFilesPath = Path.Combine(AppContext.BaseDirectory, "Web");
            ApplrWebView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                WebHostName,
                webFilesPath,
                CoreWebView2HostResourceAccessKind.Allow);
            ApplrWebView.CoreWebView2.WebMessageReceived += WebMessageReceived;
            ApplrWebView.CoreWebView2.Navigate($"https://{WebHostName}/index.html");
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"The Applr interface could not be loaded. {ex.Message}",
                "Applr",
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
        ApplrWebView.CoreWebView2?.PostWebMessageAsJson(
            JsonSerializer.Serialize(message, WebJsonOptions));
    }
}
