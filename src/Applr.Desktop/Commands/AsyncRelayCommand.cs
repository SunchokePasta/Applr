using System.Windows.Input;

namespace Applr.Desktop.Commands;

public sealed class AsyncRelayCommand : ICommand
{
    private readonly Func<Task> _execute;
    private readonly Func<bool>? _canExecute;
    private readonly Action<Exception>? _onError;
    private bool _isExecuting;

    /// <param name="onError">
    /// Optional. Given one, a failed execution is handed to it and the
    /// command returns normally. Given none, the exception is rethrown
    /// onto the dispatcher, where App's DispatcherUnhandledException
    /// handler logs it -- which is the point of the change: before, the
    /// try/finally here had no catch at all, so a throw from an
    /// <c>async void</c> Execute went straight past WPF and took the
    /// process down with the default CLR crash dialog.
    /// </param>
    public AsyncRelayCommand(
        Func<Task> execute,
        Func<bool>? canExecute = null,
        Action<Exception>? onError = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
        _onError = onError;
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter)
    {
        return !_isExecuting && (_canExecute?.Invoke() ?? true);
    }

    public async void Execute(object? parameter)
    {
        if (!CanExecute(parameter))
        {
            return;
        }

        try
        {
            _isExecuting = true;
            RaiseCanExecuteChanged();

            await _execute();
        }
        catch (Exception exception) when (_onError is not null)
        {
            // The exception filter matters: with no handler supplied,
            // this catch doesn't run at all and the exception keeps its
            // original stack trace on the way to the dispatcher.
            // The ! is because nullable flow analysis doesn't read the
            // `when` clause, not because the field can be null here.
            _onError!(exception);
        }
        finally
        {
            _isExecuting = false;
            RaiseCanExecuteChanged();
        }
    }

    public void RaiseCanExecuteChanged()
    {
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}
