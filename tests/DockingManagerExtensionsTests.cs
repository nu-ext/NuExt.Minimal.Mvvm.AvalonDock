using System.Reflection;
using System.Runtime.CompilerServices;
using AvalonDock;
using AvalonDock.Core;
using AvalonDock.Layout;

namespace Minimal.Mvvm.Wpf.Tests;

[TestClass]
public sealed class DockingManagerExtensionsTests
{
    private const BindingFlags ReflectedMember = BindingFlags.Instance | BindingFlags.NonPublic;

    [TestMethod]
    public void StaticConstructor_ResolvesReflectedAvalonDockMethods()
    {
        // The extension class resolves AvalonDock members in static fields, so a renamed or removed member
        // makes type initialization fail with MissingMethodException before any extension method runs.
        try
        {
            RuntimeHelpers.RunClassConstructor(typeof(DockingManagerExtensions).TypeHandle);
        }
        catch (Exception exception)
        {
            Assert.Fail($"{nameof(DockingManagerExtensions)} failed to initialize: {exception}");
        }
    }

    [TestMethod]
    public void InternalsAreVisibleToTests()
    {
        // Proves that the InternalsVisibleTo key of the tested assembly matches the key this test assembly is signed with.
        Assert.AreEqual(nameof(IAsyncDocumentManagerService.Count), EventArgsCache.CountPropertyChanged.PropertyName);
        Assert.AreEqual(nameof(IAsyncDocument.Title), EventArgsCache.TitlePropertyChanged.PropertyName);
        Assert.AreEqual(nameof(IViewModelProvider.ViewModel), EventArgsCache.ViewModelPropertyChanged.PropertyName);
    }

    [TestMethod]
    public void ReflectedAvalonDockMethods_Exist()
    {
        AssertPrivateMethod(typeof(ToggleDockingManager), "GetAnchorableZone", typeof(DockZone), typeof(LayoutAnchorable));
        AssertPrivateMethod(typeof(ToggleDockingManager), "RemoveFromAllBars", typeof(void), typeof(LayoutAnchorable));
    }

    [TestMethod]
    public void ExtensionMethods_ExistWithExpectedSignatures()
    {
        AssertExtensionMethod(nameof(DockingManagerExtensions.GetCurrentAnchorableZone), typeof(DockZone),
            typeof(ToggleDockingManager), typeof(LayoutAnchorable));
        AssertExtensionMethod(nameof(DockingManagerExtensions.RemoveAnchorableButton), typeof(void),
            typeof(ToggleDockingManager), typeof(LayoutAnchorable));
        AssertExtensionMethod(nameof(DockingManagerExtensions.AddAnchorable), typeof(void),
            typeof(ToggleDockingManager), typeof(LayoutAnchorable), typeof(ToolWindowPlacement));
        AssertExtensionMethod(nameof(DockingManagerExtensions.GetLayoutDocumentPane), typeof(LayoutDocumentPane),
            typeof(DockingManager));
        AssertExtensionMethod(nameof(DockingManagerExtensions.GetLayoutAnchorablePane), typeof(LayoutAnchorablePane),
            typeof(DockingManager));
        AssertExtensionMethod(nameof(DockingManagerExtensions.GetLayoutAnchorablePane), typeof(LayoutAnchorablePane),
            typeof(DockingManager), typeof(string));
    }

    private static void AssertExtensionMethod(string name, Type returnType, params Type[] parameterTypes)
    {
        string signature = string.Join(", ", parameterTypes.Select(static type => type.Name));
        var method = typeof(DockingManagerExtensions).GetMethod(name, BindingFlags.Public | BindingFlags.Static,
            binder: null, parameterTypes, modifiers: null);

        if (method is null)
        {
            Assert.Fail($"{nameof(DockingManagerExtensions)}.{name}({signature}) is missing.");
            return;
        }

        Assert.IsTrue(method.IsDefined(typeof(ExtensionAttribute), inherit: false),
            $"{nameof(DockingManagerExtensions)}.{name} must be an extension method.");
        Assert.AreEqual(parameterTypes[0], method.GetParameters()[0].ParameterType,
            $"{nameof(DockingManagerExtensions)}.{name} extends an unexpected type.");
        Assert.AreEqual(returnType, method.ReturnType,
            $"{nameof(DockingManagerExtensions)}.{name} returns an unexpected type.");
    }

    private static void AssertPrivateMethod(Type type, string name, Type returnType, params Type[] parameterTypes)
    {
        string signature = string.Join(", ", parameterTypes.Select(static parameterType => parameterType.Name));
        var method = type.GetMethod(name, ReflectedMember, binder: null, parameterTypes, modifiers: null);

        if (method is null)
        {
            Assert.Fail($"{type.Name}.{name}({signature}) is missing: the reflection inside {nameof(DockingManagerExtensions)} will fail.");
            return;
        }

        Assert.AreEqual(returnType, method.ReturnType, $"{type.Name}.{name} returns an unexpected type.");
    }
}
