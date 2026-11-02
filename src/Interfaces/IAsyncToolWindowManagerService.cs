namespace Minimal.Mvvm.Wpf;

/// <summary>
/// Represents the event arguments for the ActiveToolWindowChanged event.
/// </summary>
/// <param name="OldToolWindow">The previously active tool window.</param>
/// <param name="NewToolWindow">The newly active tool window.</param>
public readonly record struct ActiveToolWindowChangedEventArgs(IToolWindow? OldToolWindow, IToolWindow? NewToolWindow);

/// <summary>
/// Defines an interface for asynchronously creating and managing tool windows.
/// </summary>
public interface IAsyncToolWindowManagerService : INamedService
{
    /// <summary>
    /// Creates a new tool window asynchronously.
    /// </summary>
    /// <param name="toolWindowType">The type of the tool window.</param>
    /// <param name="viewModel">The view model associated with the tool window.</param>
    /// <param name="parentViewModel">The parent view model of the tool window.</param>
    /// <param name="parameter">Additional parameters for creating the tool window.</param>
    /// <param name="options">The tool window options.</param>
    /// <param name="cancellationToken">Optional cancellation token to cancel the initialization process.</param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains the created tool window.
    /// </returns>
    /// <remarks>
    /// Ownership of the view model is transferred to the manager only after the tool window is created successfully.
    /// The manager disposes the view model when the tool window manager is disposed.
    /// </remarks>
    /// <exception cref="ArgumentNullException"> Thrown if <paramref name="options"/> is <see langword="null"/>. </exception>
    /// <exception cref="OperationCanceledException">Thrown if the operation is cancelled.</exception>
    ValueTask<IToolWindow> CreateToolWindowAsync(string? toolWindowType, object? viewModel, object? parentViewModel,
        object? parameter, ToolWindowOptions options, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets or sets the currently active tool window.
    /// </summary>
    IToolWindow? ActiveToolWindow { get; set; }

    /// <summary>
    /// Occurs when the active tool window has changed.
    /// </summary>
    event EventHandler<ActiveToolWindowChangedEventArgs>? ActiveToolWindowChanged;

    /// <summary>
    /// Gets the count of tool windows.
    /// </summary>
    int Count { get; }

    /// <summary>
    /// Gets a collection of all tool windows.
    /// </summary>
    IEnumerable<IToolWindow> ToolWindows { get; }

    /// <summary>
    /// Hides all tool windows.
    /// </summary>
    void HideAll();
}