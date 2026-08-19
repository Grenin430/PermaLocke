using System.Windows;

namespace PermaLocke.App.Services;

/// <summary>
/// Runs work on the UI thread. View models need this because domain services complete their
/// awaits on thread pool threads, and touching a bound ObservableCollection from there throws.
/// </summary>
public interface IUiDispatcher
{
    Task InvokeAsync(Func<Task> action);
}

public sealed class WpfUiDispatcher : IUiDispatcher
{
    public Task InvokeAsync(Func<Task> action)
    {
        var dispatcher = Application.Current?.Dispatcher;

        if (dispatcher is null || dispatcher.CheckAccess())
        {
            return action();
        }

        return dispatcher.InvokeAsync(action).Task.Unwrap();
    }
}
