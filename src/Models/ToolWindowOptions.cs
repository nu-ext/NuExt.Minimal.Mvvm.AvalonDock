namespace Minimal.Mvvm.Wpf;

/// <summary>
/// Specifies the side where a tool window is initially placed.
/// </summary>
public enum ToolWindowPlacement
{
    Left,
    Right,
    Bottom
}

/// <summary>
/// Specifies options for creating a tool window.
/// </summary>
public sealed class ToolWindowOptions
{
    /// <summary>
    /// Gets the unique identifier of the tool window.
    /// </summary>
    public required object Id { get; init; }

    /// <summary>
    /// Gets the initial title of the tool window.
    /// </summary>
    public string? Title { get; init; }

    /// <summary>
    /// Gets the icon content displayed for the tool window.
    /// </summary>
    public object? Icon { get; init; }

    /// <summary>
    /// Gets a value indicating whether the tool window can be moved to a floating window.
    /// </summary>
    public bool CanFloat { get; init; } = true;

    /// <summary>
    /// Gets a value indicating whether the tool window can be automatically hidden.
    /// </summary>
    public bool CanAutoHide { get; init; } = true;

    /// <summary>
    /// Gets a value indicating whether the tool window can be docked as a document.
    /// </summary>
    public bool CanDockAsTabbedDocument { get; init; }

    /// <summary>
    /// Gets the name of the target anchorable pane, or <see langword="null"/> to use the active or first available pane.
    /// </summary>
    public string? PaneName { get; init; }

    /// <summary>
    /// Gets the initial placement of the tool window.
    /// </summary>
    public ToolWindowPlacement Placement { get; init; }
}