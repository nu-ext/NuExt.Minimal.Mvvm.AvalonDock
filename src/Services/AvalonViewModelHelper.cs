using System.Diagnostics;
using AvalonDock.Layout;

namespace Minimal.Mvvm.Wpf;

public static class AvalonViewModelHelper
{
    /// <summary>
    /// Detaches the current content from the specified <see cref="LayoutContent"/>.
    /// </summary>
    /// <param name="container">The container from which to detach content. Must have dispatcher access.</param>
    /// <exception cref="ArgumentNullException"><paramref name="container"/> is <see langword="null"/>.</exception>
    public static void DetachContentFromContainer(LayoutContent container)
    {
        ArgumentNullException.ThrowIfNull(container);
        Debug.Assert(container.CheckAccess());

        var view = container.Content;
        Debug.Assert(view != null);
        //First, detach DataContext from view
        ViewModelHelper.DetachViewModel(view);
        //Second, detach Content from container
        container.Content = null;
    }
}