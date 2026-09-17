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
    private const double JobPreviewWidthPixels = 540;
    private static readonly JsonSerializerOptions WebJsonOptions =
        new(JsonSerializerDefaults.Web);
    private readonly MainWindowViewModel _viewModel;
    private readonly ILogger<MainWindow> _logger;

    // Created lazily on the first job link click, not at startup -- most
    // sessions may never open the preview pane. Kept separate from
    // ApplrWebView's (default) environment on purpose: see the comment
    // on JobPreviewColumn in MainWindow.xaml.
    private CoreWebView2Environment? _jobPreviewEnvironment;

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
            if (!message.RootElement.TryGetProperty("type", out var type))
            {
                return;
            }

            switch (type.GetString())
            {
                case "loadJobs":
                    var (existingJobs, newJobs) = await _viewModel.LoadJobsAsync();
                    SendMessageToWebUi(new { type = "jobsLoaded", existingJobs, newJobs });
                    break;

                case "openJobLink":
                    // Handled entirely on this side -- there is no
                    // matching "jobLinkOpened"/"jobLinkFailed" reply,
                    // because the pane it opens into is native UI
                    // (JobPreviewWebView), not something the web content
                    // renders. OpenJobPreviewAsync swallows its own
                    // failures for the same reason PromoteNewJobsAsync
                    // does: a failed preview shouldn't disturb the jobs
                    // table this message came from.
                    if (message.RootElement.TryGetProperty("url", out var urlElement) &&
                        urlElement.GetString() is { Length: > 0 } url)
                    {
                        await OpenJobPreviewAsync(url);
                    }

                    break;
            }
        }
        catch (ApplrApiException exception)
        {
            // Already logged, already phrased for a human, already
            // carrying a reference -- the whole chain from Applr.RestApi
            // through Applr.API through ApplrClient exists so that this
            // block has nothing left to decide. Only the "loadJobs" case
            // can throw this.
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

    /// <summary>
    /// Creates the job preview pane's CoreWebView2Environment on first
    /// use, with its own user data folder under %LOCALAPPDATA% -- same
    /// fallback location logging already uses for Applr.Desktop, kept
    /// under a distinct subfolder so this pane's cookies/cache/storage
    /// never mix with ApplrWebView's default environment.
    /// </summary>
    private async Task EnsureJobPreviewInitializedAsync()
    {
        if (JobPreviewWebView.CoreWebView2 is not null)
        {
            return;
        }

        if (_jobPreviewEnvironment is null)
        {
            var userDataFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Applr",
                "WebView2",
                "JobPreview");

            var options = new CoreWebView2EnvironmentOptions(
                "--remote-debugging-port=9222"
            );

            _jobPreviewEnvironment = await CoreWebView2Environment.CreateAsync(
                browserExecutableFolder: null,
                userDataFolder: userDataFolder,
                options);
        }

        await JobPreviewWebView.EnsureCoreWebView2Async(_jobPreviewEnvironment);
    }

    private async Task OpenJobPreviewAsync(string url)
    {
        try
        {
            await EnsureJobPreviewInitializedAsync();

            JobPreviewWebView.CoreWebView2.Navigate(url);
            JobPreviewColumn.Width = new GridLength(JobPreviewWidthPixels);
        }
        catch (Exception exception)
        {
            // Swallowed rather than rethrown -- see the "openJobLink"
            // case in WebMessageReceived for why. Logged with its own
            // reference so a broken preview is still diagnosable even
            // though nothing on screen mentions it.
            var reference = ErrorReference.New();

            _logger.LogError(
                exception,
                "[{Reference}] Could not open the job preview pane for {Url}.",
                reference,
                url);

            JobPreviewColumn.Width = new GridLength(0);
        }
    }

    private void CloseJobPreviewButton_Click(object sender, RoutedEventArgs e)
    {
        JobPreviewColumn.Width = new GridLength(0);

        try
        {
            JobPreviewWebView.CoreWebView2?.Navigate("about:blank");
        }
        catch (Exception exception)
        {
            // Not worth a reference or telling the user anything -- the
            // pane is already closed either way. This only affects
            // whether it's still holding the previous page in memory
            // until it's opened again.
            _logger.LogWarning(
                exception,
                "Could not clear the job preview pane after closing it.");
        }
    }
}
