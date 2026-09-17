using Applr.Desktop.Commands;
using Applr.DesktopServices.Exceptions;
using Applr.DesktopServices.Interfaces;
using Applr.Services.Diagnostics;
using Applr.Services.DTOs;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Applr.Desktop.ViewModels;

public sealed class MainWindowViewModel : INotifyPropertyChanged
{
    private readonly IApplrClient _applrClient;
    private readonly ILogger<MainWindowViewModel> _logger;

    private bool _isLoading;
    private string _statusMessage = "Ready";

    public MainWindowViewModel(
        IApplrClient applrClient,
        ILogger<MainWindowViewModel> logger)
    {
        _applrClient = applrClient;
        _logger = logger;

        LoadJobsCommand = new AsyncRelayCommand(
            async () => await LoadJobsAsync(),
            () => !IsLoading);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>status == "New" -- the top table, shown only while non-empty.</summary>
    public ObservableCollection<JobDto> NewJobs { get; } = [];

    /// <summary>status == "Unreviewed" -- always shown beneath it (or alone).</summary>
    public ObservableCollection<JobDto> ExistingJobs { get; } = [];

    public bool IsLoading
    {
        get => _isLoading;
        private set
        {
            if (_isLoading == value)
            {
                return;
            }

            _isLoading = value;
            OnPropertyChanged();

            LoadJobsCommand.RaiseCanExecuteChanged();
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set
        {
            if (_statusMessage == value)
            {
                return;
            }

            _statusMessage = value;
            OnPropertyChanged();
        }
    }

    public AsyncRelayCommand LoadJobsCommand { get; }

    /// <summary>
    /// The on-load flow: pull both New and Unreviewed, then silently
    /// promote whatever just came back as New so it counts as Unreviewed
    /// next time round. db-sync is no longer called from here -- it's
    /// now Scraper.py's job, fired right after it upserts raw_jobs, so
    /// jobs exist on the scraper's own schedule rather than only when
    /// this app happens to be open. This just reads whatever's already
    /// there.
    ///
    /// Every failure leaves here as an ApplrApiException, so the caller
    /// (MainWindow) has exactly one exception type to translate into a
    /// message for the web UI.
    /// </summary>
    public async Task<(IReadOnlyList<JobDto> ExistingJobs, IReadOnlyList<JobDto> NewJobs)> LoadJobsAsync()
    {
        try
        {
            IsLoading = true;
            StatusMessage = "Loading jobs...";

            var existingTask = _applrClient.GetExistingJobsAsync();
            var newTask = _applrClient.GetNewJobsAsync();

            await Task.WhenAll(existingTask, newTask);

            // Awaited rather than .Result: if both calls fail, .Result
            // would surface an AggregateException wrapping the real
            // cause, and the ApplrApiException inside it -- the one
            // carrying the message and reference -- would never be seen
            // by MainWindow's catch.
            var existingJobs = await existingTask;
            var newJobs = await newTask;

            ExistingJobs.Clear();
            foreach (var job in existingJobs)
            {
                ExistingJobs.Add(job);
            }

            NewJobs.Clear();
            foreach (var job in newJobs)
            {
                NewJobs.Add(job);
            }

            StatusMessage = $"Loaded {ExistingJobs.Count} existing and {NewJobs.Count} new jobs.";

            _logger.LogInformation(
                "Loaded {ExistingCount} existing and {NewCount} new jobs.",
                ExistingJobs.Count,
                NewJobs.Count);

            // Auto-fired right after every load, silently -- kept as its
            // own method (rather than inlined here) so this can be moved
            // onto its own command/button later without touching
            // LoadJobsAsync at all.
            await PromoteNewJobsAsync(newJobs);

            return (existingJobs, newJobs);
        }
        catch (ApplrApiException exception)
        {
            // ApplrClient already wrote the full stack trace against this
            // reference. Logging it again here would put the same
            // failure in the file twice under two different references,
            // which is worse than useless when you're grepping. One line
            // recording that it reached this layer is enough.
            _logger.LogWarning(
                "[{Reference}] Loading jobs failed: {Message}",
                exception.Reference,
                exception.UserMessage);

            StatusMessage = exception.UserMessage;
            throw;
        }
        catch (Exception exception)
        {
            // Anything not already shaped by ApplrClient -- a bug in this
            // method, say. Log it with a fresh reference and re-shape it,
            // so callers still only ever see ApplrApiException.
            var reference = ErrorReference.New();

            _logger.LogError(
                exception,
                "[{Reference}] Failed to load jobs.",
                reference);

            StatusMessage = "Failed to load jobs.";

            throw new ApplrApiException(
                "Something went wrong loading your jobs.",
                reference,
                exception);
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Promotes the given (just-shown) New jobs to Unreviewed. A no-op
    /// when there's nothing new, so this is always safe to call after
    /// every load.
    /// </summary>
    public async Task PromoteNewJobsAsync(IReadOnlyList<JobDto> newJobs)
    {
        if (newJobs.Count == 0)
        {
            return;
        }

        try
        {
            _logger.LogInformation(
                "Promoting {JobCount} new jobs to Unreviewed.",
                newJobs.Count);

            await _applrClient.PromoteNewJobsAsync(
                newJobs.Select(j => j.Id).ToList());
        }
        catch (ApplrApiException exception)
        {
            // Deliberately swallowed: a failed promote shouldn't blow up
            // an otherwise-successful load. The same jobs will just show
            // up as New again next time. Already logged with its stack
            // trace by ApplrClient, so this is one line, not a second
            // copy of the same failure.
            _logger.LogWarning(
                "[{Reference}] Failed to promote new jobs: {Message}",
                exception.Reference,
                exception.UserMessage);
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "[{Reference}] Failed to promote new jobs.",
                ErrorReference.New());
        }
    }

    private void OnPropertyChanged(
        [CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(
            this,
            new PropertyChangedEventArgs(propertyName));
    }
}
