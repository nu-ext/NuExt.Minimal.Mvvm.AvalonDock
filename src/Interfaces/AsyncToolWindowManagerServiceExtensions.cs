using System.Diagnostics;

namespace Minimal.Mvvm.Wpf;

/// <summary>
/// Provides extension methods for the <see cref="IAsyncToolWindowManagerService"/>.
/// </summary>
public static class AsyncToolWindowManagerServiceExtensions
{
    /// <summary>
    /// Finds a tool window by its ID.
    /// </summary>
    /// <param name="service">The tool window manager service to search in.</param>
    /// <param name="id">The ID of the tool window to find.</param>
    /// <returns>The found tool window, or <see langword="null"/> if no tool window with the specified ID is found.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="service"/> is null.</exception>
    public static IToolWindow? FindToolWindowById(this IAsyncToolWindowManagerService service, object id)
    {
        ArgumentNullException.ThrowIfNull(service);

        return service.ToolWindows.FirstOrDefault(x => Equals(x.Id, id));
    }

    /// <summary>
    /// Creates a tool window with a new ViewModel.
    /// </summary>
    /// <typeparam name="TViewModel">The type of the tool window ViewModel.</typeparam>
    /// <param name="service">The tool window manager service.</param>
    /// <param name="toolWindowType">The type of the tool window.</param>
    /// <param name="viewModelFactory">The factory that creates the tool window ViewModel.</param>
    /// <param name="parentViewModel">The parent ViewModel.</param>
    /// <param name="parameter">The initialization parameter.</param>
    /// <param name="options">The tool window options.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The newly created tool window.</returns>
    /// <remarks>
    /// The newly created ViewModel remains owned by this operation until tool window creation succeeds.
    /// The tool window manager takes ownership after successful creation and disposes the ViewModel when the manager is disposed.
    /// </remarks>
    public static async ValueTask<IToolWindow> CreateToolWindowAsync<TViewModel>(
        this IAsyncToolWindowManagerService service,
        string? toolWindowType,
        Func<TViewModel> viewModelFactory,
        object? parentViewModel,
        object? parameter,
        ToolWindowOptions options,
        CancellationToken cancellationToken = default)
        where TViewModel : class, IAsyncDisposable
    {
        ArgumentNullException.ThrowIfNull(service);
        ArgumentNullException.ThrowIfNull(viewModelFactory);
        ArgumentNullException.ThrowIfNull(options);
        cancellationToken.ThrowIfCancellationRequested();

        var viewModel = viewModelFactory();

        try
        {
            return await service.CreateToolWindowAsync(
                toolWindowType: toolWindowType,
                viewModel: viewModel,
                parentViewModel: parentViewModel,
                parameter: parameter,
                options: options,
                cancellationToken: cancellationToken);
        }
        catch
        {
            try
            {
                await viewModel.DisposeAsync();
            }
            catch
            {
                var message = $"Failed to dispose the ViewModel {viewModel.GetType().FullName} after tool window {toolWindowType} creation failed.";

                Trace.WriteLine(message);
                Debug.Fail(message);
            }

            throw;
        }
    }
}