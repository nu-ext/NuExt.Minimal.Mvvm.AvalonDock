using System.Reflection;
using AvalonDock;
using AvalonDock.Core;
using AvalonDock.Layout;

namespace Minimal.Mvvm.Wpf;

public static class DockingManagerExtensions
{
    private static readonly MethodInfo s_getAnchorableZoneMethod = typeof(ToggleDockingManager)
        .GetMethod("GetAnchorableZone", BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new MissingMethodException(typeof(ToggleDockingManager).FullName, "GetAnchorableZone");

    public static DockZone GetCurrentAnchorableZone(this ToggleDockingManager manager, LayoutAnchorable anchorable)
    {
        ArgumentNullException.ThrowIfNull(manager);
        ArgumentNullException.ThrowIfNull(anchorable);

        return (DockZone)s_getAnchorableZoneMethod.Invoke(manager, [anchorable])!;
    }

    private static readonly MethodInfo s_removeFromAllBarsMethod = typeof(ToggleDockingManager).GetMethod(
        "RemoveFromAllBars", BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new MissingMethodException(typeof(ToggleDockingManager).FullName, "RemoveFromAllBars");

    public static void RemoveAnchorableButton(this ToggleDockingManager manager, LayoutAnchorable anchorable)
    {
        ArgumentNullException.ThrowIfNull(manager);
        ArgumentNullException.ThrowIfNull(anchorable);
        //anchorable.Hide();
        s_removeFromAllBarsMethod.Invoke(manager, [anchorable]);
    }

    /// <summary>
    /// Gets the document pane associated with the active or last focused document, or the first available document pane.
    /// </summary>
    /// <param name="manager">The docking manager containing the layout.</param>
    /// <returns>
    /// The active, last focused, or first available document pane; otherwise, <see langword="null"/> if the layout contains no document panes.
    /// </returns>
    public static LayoutDocumentPane? GetLayoutDocumentPane(this DockingManager manager)
    {
        ArgumentNullException.ThrowIfNull(manager);

        var layout = manager.Layout;

        if (layout?.ActiveContent is LayoutDocument { Parent: LayoutDocumentPane activePane })
            return activePane;

        return layout?.LastFocusedDocument?.Parent as LayoutDocumentPane
               ?? layout?.Descendents().OfType<LayoutDocumentPane>().FirstOrDefault();
    }

    /// <summary>
    /// Gets the active or first available anchorable pane.
    /// </summary>
    /// <param name="manager">The docking manager containing the layout.</param>
    /// <returns>The active or first available anchorable pane, or <see langword="null"/> if none exists.</returns>
    public static LayoutAnchorablePane? GetLayoutAnchorablePane(this DockingManager manager)
    {
        ArgumentNullException.ThrowIfNull(manager);

        var layout = manager.Layout;

        if (layout?.ActiveContent is LayoutAnchorable { Parent: LayoutAnchorablePane activePane })
            return activePane;

        return layout?.Descendents().OfType<LayoutAnchorablePane>().FirstOrDefault();
    }

    /// <summary>
    /// Gets an anchorable pane by its name.
    /// </summary>
    /// <param name="manager">The docking manager containing the layout.</param>
    /// <param name="paneName">The name of the anchorable pane.</param>
    /// <returns>The matching anchorable pane, or <see langword="null"/> if no matching pane exists.</returns>
    public static LayoutAnchorablePane? GetLayoutAnchorablePane(this DockingManager manager, string paneName)
    {
        ArgumentNullException.ThrowIfNull(manager);
        ArgumentException.ThrowIfNullOrEmpty(paneName);

        return manager.Layout?.Descendents().OfType<LayoutAnchorablePane>().FirstOrDefault(x => x.Name == paneName);
    }

    /// <summary>
    /// Adds an anchorable to the specified side of a toggle docking manager.
    /// </summary>
    /// <param name="manager">The toggle docking manager.</param>
    /// <param name="anchorable">The anchorable to add.</param>
    /// <param name="placement">The side where the anchorable should be placed.</param>
    public static void AddAnchorable(this ToggleDockingManager manager, LayoutAnchorable anchorable,
        ToolWindowPlacement placement)
    {
        ArgumentNullException.ThrowIfNull(manager);
        ArgumentNullException.ThrowIfNull(anchorable);

        Throw.InvalidOperationExceptionIf(manager.IsLoaded,
            "Tool windows must be added before ToggleDockingManager is loaded.");

        var side = placement switch
        {
            ToolWindowPlacement.Left => manager.Layout.LeftSide,
            ToolWindowPlacement.Right => manager.Layout.RightSide,
            ToolWindowPlacement.Bottom => manager.Layout.BottomSide,
            _ => throw new ArgumentOutOfRangeException(nameof(placement))
        };

        var group = side.Children.FirstOrDefault();
        if (group is null)
        {
            group = new LayoutAnchorGroup();
            side.Children.Add(group);
        }

        group.Children.Add(anchorable);
    }
}