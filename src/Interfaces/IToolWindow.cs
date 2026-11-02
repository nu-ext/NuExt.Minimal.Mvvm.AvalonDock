namespace Minimal.Mvvm.Wpf;

/// <summary>
/// Represents a tool window.
/// </summary>
public interface IToolWindow
{
    /// <summary>
    /// Gets or sets the unique identifier of the tool window.
    /// </summary>
    object Id { get; set; }

    /// <summary>
    /// Gets or sets the title of the tool window.
    /// </summary>
    string Title { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the tool window can be moved to a floating window.
    /// </summary>
    bool CanFloat { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the tool window can be automatically hidden.
    /// </summary>
    bool CanAutoHide { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the tool window can be docked as a document.
    /// </summary>
    bool CanDockAsTabbedDocument { get; set; }

    /// <summary>
    /// Hides the tool window.
    /// </summary>
    void Hide();

    /// <summary>
    /// Shows and activates the tool window.
    /// </summary>
    void Show();
}