using System.IO;
using System.Windows;
using System.Windows.Threading;
using Applr.Desktop.ViewModels;
using Applr.DesktopServices.Clients;
using Applr.DesktopServices.Interfaces;
using Applr.Services.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;

namespace Applr.Desktop;

public partial class App : System.Windows.Application
{
    private const string LogTemplate =
        "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext} {Message:lj}{NewLine}{Exception}";

    private IHost? _host;
    private ILogger<App>? _logger;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Serilog is configured before the host is even built, so that a
        // failure during host construction (a missing appsettings.json,
        // a bad ApiBaseUrl) still reaches the log file rather than
        // vanishing. Serilog.Log is the static fallback logger the
        // handlers below use for exactly that window.
        Log.Logger = BuildLogger();

        // These three cover every route an exception can take out of a
        // WPF app. Without them, an unhandled exception on any of them
        // closes the window with the default CLR crash dialog and
        // nothing is written down.
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        try
        {
            _host = Host.CreateDefaultBuilder()
                // Without this, appsettings.json is resolved against the
                // process's *current directory*. Launch the exe from a
                // shortcut, a scheduled task or a different drive and
                // ApiBaseUrl silently comes back null -- which surfaced
                // as "ApiBaseUrl is not configured" from a build that
                // very much does ship an appsettings.json.
                .UseContentRoot(AppContext.BaseDirectory)
                .UseSerilog()
                .ConfigureServices((context, services) =>
                {
                    services.AddHttpClient<IApplrClient, ApplrClient>(
                        client =>
                        {
                            var baseUrl = context.Configuration["ApiBaseUrl"];

                            if (string.IsNullOrWhiteSpace(baseUrl))
                            {
                                throw new InvalidOperationException(
                                    "ApiBaseUrl is not configured.");
                            }

                            client.BaseAddress = new Uri(baseUrl);

                            // Shorter than HttpClient's 100s default so a
                            // hung API becomes a visible error while the
                            // user still cares.
                            client.Timeout = TimeSpan.FromSeconds(30);
                        });

                    services.AddSingleton<MainWindowViewModel>();
                    services.AddSingleton<MainWindow>();
                })
                .Build();

            _logger = _host.Services.GetRequiredService<ILogger<App>>();

            await _host.StartAsync();

            _logger.LogInformation("Applr started.");

            var mainWindow =
                _host.Services.GetRequiredService<MainWindow>();

            mainWindow.Show();
        }
        catch (Exception exception)
        {
            // OnStartup is async void: an exception here has no caller to
            // propagate to and would otherwise take the process down with
            // no window and no explanation.
            var reference = ErrorReference.New();

            Log.Error(
                exception,
                "[{Reference}] Applr could not start.",
                reference);

            MessageBox.Show(
                $"Applr could not start.{Environment.NewLine}{Environment.NewLine}" +
                $"{exception.Message}{Environment.NewLine}{Environment.NewLine}" +
                $"Reference: {reference}",
                "Applr",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            Shutdown(-1);
        }
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        try
        {
            if (_host is not null)
            {
                await _host.StopAsync();
                _host.Dispose();
            }
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Applr did not shut down cleanly.");
        }
        finally
        {
            // Flushes anything still buffered in the file sink. Skip this
            // and the last few lines before a crash -- the interesting
            // ones -- never reach disk.
            Log.CloseAndFlush();
        }

        base.OnExit(e);
    }

    /// <summary>
    /// Console + a rolling daily file under ./logs, anchored to the app's
    /// own folder rather than the process's working directory -- the same
    /// arrangement as Applr.API and Applr.RestApi, so all three logs sit
    /// beside their binaries and can be read side by side.
    ///
    /// Falls back to %LOCALAPPDATA%\Applr\logs when the app folder isn't
    /// writable (an install under Program Files), because a logger that
    /// throws while being constructed is worse than one in a second-
    /// choice location.
    /// </summary>
    private static Serilog.ILogger BuildLogger()
    {
        var logDirectory = ResolveLogDirectory();

        return new LoggerConfiguration()
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("System.Net.Http.HttpClient", LogEventLevel.Information)
            .Enrich.FromLogContext()
            .WriteTo.Debug(outputTemplate: LogTemplate)
            .WriteTo.File(
                Path.Combine(logDirectory, "log-.txt"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 14,
                outputTemplate: LogTemplate,
                shared: true)
            .CreateLogger();
    }

    private static string ResolveLogDirectory()
    {
        var preferred = Path.Combine(AppContext.BaseDirectory, "logs");

        try
        {
            Directory.CreateDirectory(preferred);

            // CreateDirectory succeeding doesn't prove the folder is
            // writable (it may already exist and be locked down), so
            // prove it.
            var probe = Path.Combine(preferred, $".write-probe-{Guid.NewGuid():N}");
            File.WriteAllText(probe, string.Empty);
            File.Delete(probe);

            return preferred;
        }
        catch (Exception)
        {
            var fallback = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Applr",
                "logs");

            Directory.CreateDirectory(fallback);

            return fallback;
        }
    }

    /// <summary>
    /// An exception that escaped a UI-thread handler (a click, a binding,
    /// an async void handler). Handled = true keeps the app alive: a
    /// failed load is not a reason to lose the window.
    /// </summary>
    private void OnDispatcherUnhandledException(
        object sender,
        DispatcherUnhandledExceptionEventArgs e)
    {
        var reference = ErrorReference.New();

        Log.Error(
            e.Exception,
            "[{Reference}] An unhandled exception reached the UI thread.",
            reference);

        MessageBox.Show(
            $"Something went wrong.{Environment.NewLine}{Environment.NewLine}" +
            $"Reference: {reference}",
            "Applr",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);

        e.Handled = true;
    }

    /// <summary>
    /// The process is going down regardless -- this exists only so the
    /// reason is written down before it does.
    /// </summary>
    private void OnDomainUnhandledException(
        object sender,
        UnhandledExceptionEventArgs e)
    {
        // ExceptionObject is typed object and can, in theory, be a
        // non-Exception thrown from native or IL-level code.
        var exception = e.ExceptionObject as Exception
            ?? new Exception(e.ExceptionObject?.ToString() ?? "Unknown error object.");

        Log.Fatal(
            exception,
            "[{Reference}] An unhandled exception is terminating Applr. Terminating: {IsTerminating}.",
            ErrorReference.New(),
            e.IsTerminating);

        Log.CloseAndFlush();
    }

    /// <summary>
    /// A Task faulted and nobody awaited it. Not fatal, but it means an
    /// error was thrown away somewhere -- worth a line so it stops being
    /// invisible.
    /// </summary>
    private void OnUnobservedTaskException(
        object? sender,
        UnobservedTaskExceptionEventArgs e)
    {
        Log.Error(
            e.Exception,
            "[{Reference}] A faulted task was never observed.",
            ErrorReference.New());

        e.SetObserved();
    }
}
