using System.IO;
using System.Text.Json;
using System.Windows;
using Applr.Desktop.ViewModels;
using Applr.DesktopServices.Exceptions;
using Applr.Services.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Web.WebView2.Core;

namespace Applr.Desktop;

public partial class MainWindow : Window
{
    private const string WebHostName = "applr.local";
    private static readonly JsonSerializerOptions WebJsonOptions =
        new(JsonSerializerDefaults.Web);
    private readonly MainWindowViewModel _viewModel;
    private readonly ILogger<MainWindow> _logger;

    public MainWindow(
        MainWindowViewModel viewModel,
        ILogger<MainWindow> logger)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _logger = logger;
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
        catch (Exception exception)
        {
            // Nothing is rendered yet, so there is no web UI to post an
            // error into -- a MessageBox is the only surface left. The
            // log line is what makes it diagnosable afterwards; before,
            // only ex.Message survived, in a dialog the user dismissed.
            var reference = ErrorReference.New();

            _logger.LogError(
                exception,
                "[{Reference}] The WebView2 host could not be initialised.",
                reference);

            MessageBox.Show(
                $"The Applr interface could not be loaded.{Environment.NewLine}{Environment.NewLine}" +
                $"{exception.Message}{Environment.NewLine}{Environment.NewLine}" +
                $"Reference: {reference}",
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

            var (existingJobs, newJobs) = await _viewModel.LoadJobsAsync();
            SendMessageToWebUi(new { type = "jobsLoaded", existingJobs, newJobs });
        }
        catch (ApplrApiException exception)
        {
            // Already logged, already phrased for a human, already
            // carrying a reference -- the whole chain from Applr.RestApi
            // through Applr.API through ApplrClient exists so that this
            // block has nothing left to decide.
            SendMessageToWebUi(new
            {
                type = "jobsFailed",
                message = exception.UserMessage,
                reference = exception.Reference
            });
        }
        catch (Exception exception)
        {
            // Anything that isn't a known API failure: a JSON parse
            // problem on the message from the WebView, a bug in the view
            // model. Mint a reference so the generic message on screen
            // still points at a specific stack trace in the log.
            var reference = ErrorReference.New();

            _logger.LogError(
                exception,
                "[{Reference}] Handling a message from the web UI failed.",
                reference);

            SendMessageToWebUi(new
            {
                type = "jobsFailed",
                message = "Something went wrong loading your jobs.",
                reference
            });
        }
    }

    private void SendMessageToWebUi(object message)
    {
        try
        {
            ApplrWebView.CoreWebView2?.PostWebMessageAsJson(
                JsonSerializer.Serialize(message, WebJsonOptions));
        }
        catch (Exception exception)
        {
            // Posting the *error* message failed -- typically because
            // the WebView is already tearing down. Swallowing is right
            // here (there is no third place to report to), but it must
            // not be silent.
            _logger.LogError(
                exception,
                "Could not post a message to the web UI.");
        }
    }
}
