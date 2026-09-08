using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using JobFinder.Desktop.Commands;
using JobFinder.DesktopServices.Interfaces;
using Microsoft.Extensions.Logging;

namespace JobFinder.Desktop.ViewModels;

public sealed class MainWindowViewModel : INotifyPropertyChanged
{
    private readonly IJobFinderClient _jobFinderClient;
    private readonly ILogger<MainWindowViewModel> _logger;

    private string _connectionStatus = "Not connected";
    private bool _isConnecting;

    public MainWindowViewModel(
        IJobFinderClient jobFinderClient,
        ILogger<MainWindowViewModel> logger)
    {
        _jobFinderClient = jobFinderClient;
        _logger = logger;

        TestConnectionCommand = new AsyncRelayCommand(
            TestConnectionAsync,
            () => !IsConnecting);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string ConnectionStatus
    {
        get => _connectionStatus;
        private set
        {
            if (_connectionStatus == value)
            {
                return;
            }

            _connectionStatus = value;
            OnPropertyChanged();
        }
    }

    public bool IsConnecting
    {
        get => _isConnecting;
        private set
        {
            if (_isConnecting == value)
            {
                return;
            }

            _isConnecting = value;
            OnPropertyChanged();

            TestConnectionCommand.RaiseCanExecuteChanged();
        }
    }

    public AsyncRelayCommand TestConnectionCommand { get; }

    private async Task TestConnectionAsync()
    {
        try
        {
            IsConnecting = true;
            ConnectionStatus = "Connecting...";

            _logger.LogInformation(
                "Testing connection to JobFinder API.");

            var status =
                await _jobFinderClient.GetJobsAsync();

            ConnectionStatus = status;

            _logger.LogInformation(
                "JobFinder API connection test completed with status: {Status}",
                status);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to connect to JobFinder API.");

            ConnectionStatus = "Connection failed";
        }
        finally
        {
            IsConnecting = false;
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
