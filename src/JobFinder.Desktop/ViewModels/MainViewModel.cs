using JobFinder.Desktop.Commands;
using JobFinder.DesktopServices.Interfaces;
using JobFinder.Services.DTOs;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace JobFinder.Desktop.ViewModels;

public sealed class MainWindowViewModel : INotifyPropertyChanged
{
    private readonly IJobFinderClient _jobFinderClient;
    private readonly ILogger<MainWindowViewModel> _logger;

    private bool _isLoading;
    private string _statusMessage = "Ready";

    public MainWindowViewModel(
        IJobFinderClient jobFinderClient,
        ILogger<MainWindowViewModel> logger)
    {
        _jobFinderClient = jobFinderClient;
        _logger = logger;

        LoadJobsCommand = new AsyncRelayCommand(
            async () => await LoadJobsAsync(),
            () => !IsLoading);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<DbJobDto> Jobs { get; } = [];

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

    public async Task<IReadOnlyList<DbJobDto>> LoadJobsAsync()
    {
        try
        {
            IsLoading = true;
            StatusMessage = "Loading jobs...";

            _logger.LogInformation(
                "Requesting scraped jobs from JobFinder API.");

            var jobs =
                await _jobFinderClient.GetJobsAsync();

            Jobs.Clear();

            foreach (var job in jobs)
            {
                Jobs.Add(job);
            }

            StatusMessage = $"Loaded {Jobs.Count} jobs.";

            _logger.LogInformation(
                "Loaded {JobCount} scraped jobs.",
                Jobs.Count);

            return jobs;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to load scraped jobs.");

            StatusMessage = "Failed to load jobs.";
            throw;
        }
        finally
        {
            IsLoading = false;
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
