using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using Applr.Desktop.ViewModels;
using Applr.DesktopServices.Exceptions;
using Applr.DesktopServices.Interfaces;
using Applr.Services.Diagnostics;
using Applr.Services.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Web.WebView2.Core;

namespace Applr.Desktop;

public partial class MainWindow : Window
{
    private const string WebHostName = "applr.local";
    private const double SplitterWidthPixels = 6;
    // Matches ApplrColumn's MinWidth in the XAML. Held here too because
    // full screen has to drop it to zero and put it back afterwards.
    private const double ApplrMinimumWidthPixels = 380;
    private const string FullScreenGlyph = "";
    private const string ExitFullScreenGlyph = "";
    private static readonly JsonSerializerOptions WebJsonOptions =
        new(JsonSerializerDefaults.Web);
    private readonly MainWindowViewModel _viewModel;
    private readonly IApplrFillerClient _fillerClient;
    private readonly JobPreviewOptions _jobPreviewOptions;
    private readonly ILogger<MainWindow> _logger;

    // Created lazily on the first job link click, not at startup -- most
    // sessions may never open the preview pane. Kept separate from
    // ApplrWebView's (default) environment on purpose: see the comment
    // on JobPreviewColumn in MainWindow.xaml.
    private CoreWebView2Environment? _jobPreviewEnvironment;

    // The pane's width in its ordinary (non-full-screen) state. Held here
    // rather than read back off the column because full screen sets that
    // column to Star, which loses the pixel width the user dragged to.
    private double _jobPreviewRestoreWidth;

    private bool _jobPreviewIsFullScreen;

    public MainWindow(
        MainWindowViewModel viewModel,
        IApplrFillerClient fillerClient,
        JobPreviewOptions jobPreviewOptions,
        ILogger<MainWindow> logger)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _fillerClient = fillerClient;
        _jobPreviewOptions = jobPreviewOptions;
        _logger = logger;
        _jobPreviewRestoreWidth = jobPreviewOptions.WidthPixels;

        // Esc and F11 reach these only while focus is on the chrome bar or
        // the window itself. A WebView2 is a native child window and keeps
        // the keystrokes it receives, so with the cursor in the web content
        // the buttons are the way out -- which is why they are in a bar of
        // their own rather than overlaid on a page that paints over them.
        InputBindings.Add(new KeyBinding(
            new RelayCommand(CloseJobPreview), Key.Escape, ModifierKeys.None));
        InputBindings.Add(new KeyBinding(
            new RelayCommand(ToggleJobPreviewFullScreen), Key.F11, ModifierKeys.None));

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
                    // renders. The swallow lives in OpenJobPreviewAsync
                    // for the same reason PromoteNewJobsAsync has one: a
                    // failed preview shouldn't disturb the jobs table
                    // this message came from.
                    if (message.RootElement.TryGetProperty("url", out var urlElement) &&
                        urlElement.GetString() is { Length: > 0 } url)
                    {
                        await OpenJobPreviewAsync(url);
                    }

                    break;

                case "applyToJob":
                    // Apply now means "open this posting, then fill it".
                    // The url travels with the message so the pane lands on
                    // the right page and ApplrFiller can be told which page
                    // that is, rather than filling whichever one happened to
                    // be newest.
                    if (message.RootElement.TryGetProperty("jobId", out var jobIdElement) &&
                        jobIdElement.TryGetUInt32(out var jobId))
                    {
                        var applyUrl =
                            message.RootElement.TryGetProperty("url", out var applyUrlElement) &&
                            applyUrlElement.ValueKind == JsonValueKind.String
                                ? applyUrlElement.GetString()
                                : null;

                        await ApplyToJobAsync(jobId, applyUrl);
                    }
                    else
                    {
                        // This used to fall through in silence. The row that
                        // sent the message is already showing "Applying" and
                        // its own guard stops a second click, so no reply
                        // left it stuck until the app restarted. There is no
                        // jobId to answer to here -- the web UI's watchdog
                        // releases the row -- so this line exists to make the
                        // cause findable when it does.
                        _logger.LogError(
                            "[{Reference}] An applyToJob message arrived with no usable " +
                            "jobId and was ignored: {Message}",
                            ErrorReference.New(),
                            e.WebMessageAsJson);
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

    /// <summary>
    /// Opens the posting in the preview pane, waits for it to finish loading,
    /// then has ApplrFiller fill it and reports the outcome back to the row
    /// that asked.
    ///
    /// The wait is the point. Navigate() returns as soon as the navigation is
    /// queued, so filling straight after it would hand ApplrFiller whatever
    /// the pane had open a moment ago -- most often the previous job's form.
    ///
    /// Handles its own failures rather than letting them reach
    /// WebMessageReceived's catch blocks: those answer with "jobsFailed",
    /// which would replace the whole jobs table with an error panel because
    /// one row's fill did not work. An applyFailed carrying the same message
    /// and reference keeps the failure on the row it belongs to.
    /// </summary>
    private async Task ApplyToJobAsync(uint jobId, string? url)
    {
        try
        {
            // The url that comes back is the one the browser settled on,
            // which after a job board's redirects is often not the one asked
            // for. Passing that on is what lets ApplrFiller match the page
            // exactly instead of falling back to a guess.
            var openUrl = string.IsNullOrWhiteSpace(url)
                ? null
                : await ShowJobInPreviewAsync(url);

            var result = await _fillerClient.ApplyAsync(jobId, openUrl);

            SendMessageToWebUi(new
            {
                type = "applyFinished",
                jobId,
                filled = result.Filled,
                notFilled = result.NotFilled,
                unmatchedLabels = result.UnmatchedLabels
            });
        }
        catch (ApplrApiException exception)
        {
            SendMessageToWebUi(new
            {
                type = "applyFailed",
                jobId,
                message = exception.UserMessage,
                reference = exception.Reference
            });
        }
        catch (TimeoutException exception)
        {
            var reference = ErrorReference.New();

            _logger.LogError(
                exception,
                "[{Reference}] The posting for job {JobId} did not finish loading within " +
                "{Seconds}s, so nothing was filled.",
                reference,
                jobId,
                _jobPreviewOptions.NavigationTimeoutSeconds);

            SendMessageToWebUi(new
            {
                type = "applyFailed",
                jobId,
                message = "The job posting took too long to load, so nothing was filled.",
                reference
            });
        }
        catch (Exception exception)
        {
            var reference = ErrorReference.New();

            _logger.LogError(
                exception,
                "[{Reference}] Filling the form for job {JobId} failed.",
                reference,
                jobId);

            SendMessageToWebUi(new
            {
                type = "applyFailed",
                jobId,
                message = "Something went wrong filling the form.",
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
    ///
    /// The debugging port comes from JobPreview:RemoteDebuggingPort rather
    /// than a literal: ApplrFiller connects to it by number, and two copies
    /// of Applr on one machine cannot both hold the same one.
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
                $"--remote-debugging-port={_jobPreviewOptions.RemoteDebuggingPort}"
            );

            _jobPreviewEnvironment = await CoreWebView2Environment.CreateAsync(
                browserExecutableFolder: null,
                userDataFolder: userDataFolder,
                options);
        }

        await JobPreviewWebView.EnsureCoreWebView2Async(_jobPreviewEnvironment);
    }

    /// <summary>
    /// Navigates the pane and does not return until the page has finished
    /// loading, answering with the url the browser actually ended on.
    /// Throws on a failed or slow navigation, so a caller that depends on
    /// the page being there (Apply) can say so, and one that does not
    /// (a link click) can swallow it.
    /// </summary>
    private async Task<string> ShowJobInPreviewAsync(string url)
    {
        await EnsureJobPreviewInitializedAsync();

        RevealJobPreviewPane();
        JobPreviewAddress.Text = url;

        var completion = new TaskCompletionSource<CoreWebView2NavigationCompletedEventArgs>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        void OnNavigationCompleted(
            object? sender,
            CoreWebView2NavigationCompletedEventArgs args) =>
            completion.TrySetResult(args);

        // Top-level only: frames raise FrameNavigationCompleted instead, so
        // an ad iframe finishing first cannot be mistaken for the page.
        JobPreviewWebView.CoreWebView2.NavigationCompleted += OnNavigationCompleted;

        try
        {
            JobPreviewWebView.CoreWebView2.Navigate(url);

            var completed = await completion.Task.WaitAsync(
                TimeSpan.FromSeconds(_jobPreviewOptions.NavigationTimeoutSeconds));

            if (!completed.IsSuccess)
            {
                throw new InvalidOperationException(
                    $"The browser could not load {url} ({completed.WebErrorStatus}).");
            }
        }
        finally
        {
            JobPreviewWebView.CoreWebView2.NavigationCompleted -= OnNavigationCompleted;
        }

        var landedOn = JobPreviewWebView.CoreWebView2.Source;
        JobPreviewAddress.Text = landedOn;

        return landedOn;
    }

    private async Task OpenJobPreviewAsync(string url)
    {
        try
        {
            await ShowJobInPreviewAsync(url);
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

            CloseJobPreview();
        }
    }

    /// <summary>
    /// Puts the pane on screen at whatever width it was last left at,
    /// leaving a full-screen pane full screen.
    /// </summary>
    private void RevealJobPreviewPane()
    {
        if (_jobPreviewIsFullScreen)
        {
            return;
        }

        JobPreviewColumn.Width = new GridLength(_jobPreviewRestoreWidth);
        JobPreviewSplitterColumn.Width = new GridLength(SplitterWidthPixels);
    }

    private bool IsJobPreviewOpen =>
        _jobPreviewIsFullScreen || JobPreviewColumn.Width.Value > 0;

    private void CloseJobPreviewButton_Click(object sender, RoutedEventArgs e) =>
        CloseJobPreview();

    private void CloseJobPreview()
    {
        if (!IsJobPreviewOpen)
        {
            return;
        }

        if (_jobPreviewIsFullScreen)
        {
            SetJobPreviewFullScreen(false);
        }

        JobPreviewColumn.Width = new GridLength(0);
        JobPreviewSplitterColumn.Width = new GridLength(0);
        JobPreviewAddress.Text = string.Empty;

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

    private void ToggleJobPreviewFullScreenButton_Click(object sender, RoutedEventArgs e) =>
        ToggleJobPreviewFullScreen();

    private void ToggleJobPreviewFullScreen()
    {
        if (!IsJobPreviewOpen)
        {
            return;
        }

        SetJobPreviewFullScreen(!_jobPreviewIsFullScreen);
    }

    /// <summary>
    /// Full screen is the jobs table's column going to zero, not a window
    /// state change: the pane is one column of a Grid, so giving it all the
    /// width is the whole of it. ApplrColumn's MinWidth has to go with it,
    /// otherwise the minimum holds the table open at 380px.
    /// </summary>
    private void SetJobPreviewFullScreen(bool isFullScreen)
    {
        _jobPreviewIsFullScreen = isFullScreen;

        if (isFullScreen)
        {
            ApplrColumn.MinWidth = 0;
            ApplrColumn.Width = new GridLength(0);
            JobPreviewSplitterColumn.Width = new GridLength(0);
            JobPreviewColumn.Width = new GridLength(1, GridUnitType.Star);

            ToggleJobPreviewFullScreenButton.Content = ExitFullScreenGlyph;
            ToggleJobPreviewFullScreenButton.ToolTip = "Exit full screen";
            AutomationProperties.SetName(
                ToggleJobPreviewFullScreenButton, "Exit full screen");

            return;
        }

        ApplrColumn.MinWidth = ApplrMinimumWidthPixels;
        ApplrColumn.Width = new GridLength(1, GridUnitType.Star);
        JobPreviewColumn.Width = new GridLength(_jobPreviewRestoreWidth);
        JobPreviewSplitterColumn.Width = new GridLength(SplitterWidthPixels);

        ToggleJobPreviewFullScreenButton.Content = FullScreenGlyph;
        ToggleJobPreviewFullScreenButton.ToolTip = "Full screen";
        AutomationProperties.SetName(
            ToggleJobPreviewFullScreenButton, "Full screen");
    }

    /// <summary>
    /// The floor the splitter cannot be dragged below. It is enforced here
    /// rather than as the column's MinWidth because that same column has to
    /// be able to reach zero when the pane closes, and a MinWidth would stop
    /// it -- so the two states would fight each other.
    /// </summary>
    private void JobPreviewSplitter_DragDelta(object sender, DragDeltaEventArgs e)
    {
        if (_jobPreviewIsFullScreen)
        {
            return;
        }

        if (JobPreviewColumn.Width.Value < _jobPreviewOptions.MinimumWidthPixels)
        {
            JobPreviewColumn.Width =
                new GridLength(_jobPreviewOptions.MinimumWidthPixels);
        }

        _jobPreviewRestoreWidth = JobPreviewColumn.Width.Value;
    }
}

/// <summary>
/// The smallest thing that turns a method into an ICommand, so the window's
/// two KeyBindings can point at the same handlers the chrome bar's buttons
/// use. There is no view model involved and nothing to enable or disable --
/// both actions no-op when the pane is closed.
/// </summary>
internal sealed class RelayCommand(Action execute) : ICommand
{
    public event EventHandler? CanExecuteChanged
    {
        add { }
        remove { }
    }

    public bool CanExecute(object? parameter) => true;

    public void Execute(object? parameter) => execute();
}
