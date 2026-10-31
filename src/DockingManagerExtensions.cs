using AvalonDock;
using AvalonDock.Layout;

namespace Minimal.Mvvm.Wpf;

public static class DockingManagerExtensions
{
    public static LayoutDocumentPane? GetLayoutDocumentPane(this DockingManager manager)
    {
        ArgumentNullException.ThrowIfNull(manager);

        var layout = manager.Layout;

        if (layout?.ActiveContent is LayoutDocument { Parent: LayoutDocumentPane activePane })
            return activePane;

        return layout?.LastFocusedDocument?.Parent as LayoutDocumentPane
               ?? layout?.Descendents().OfType<LayoutDocumentPane>().FirstOrDefault();
    }
}