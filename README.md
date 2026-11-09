# NuExt.Minimal.Mvvm.AvalonDock

`NuExt.Minimal.Mvvm.AvalonDock` integrates [AvalonDock](https://github.com/Dirkster99/AvalonDock) with [`NuExt.Minimal.Mvvm.Wpf`](https://github.com/nu-ext/NuExt.Minimal.Mvvm.Wpf), providing MVVM infrastructure for dockable documents and tool windows.

[![NuGet](https://img.shields.io/nuget/v/NuExt.Minimal.Mvvm.AvalonDock.svg)](https://www.nuget.org/packages/NuExt.Minimal.Mvvm.AvalonDock)
[![Ask DeepWiki](https://img.shields.io/badge/Ask-DeepWiki-blue)](https://deepwiki.com/nu-ext/NuExt.Minimal.Mvvm.AvalonDock)
[![Build](https://github.com/nu-ext/NuExt.Minimal.Mvvm.AvalonDock/actions/workflows/ci.yml/badge.svg)](https://github.com/nu-ext/NuExt.Minimal.Mvvm.AvalonDock/actions/workflows/ci.yml)
[![License](https://img.shields.io/github/license/nu-ext/NuExt.Minimal.Mvvm.AvalonDock?label=license)](https://github.com/nu-ext/NuExt.Minimal.Mvvm.AvalonDock/blob/main/LICENSE)
[![Downloads](https://img.shields.io/nuget/dt/NuExt.Minimal.Mvvm.AvalonDock.svg)](https://www.nuget.org/packages/NuExt.Minimal.Mvvm.AvalonDock)

## Compatibility

- WPF on **.NET 8/9/10** and **.NET Framework 4.8+**
- AvalonDock 5

## Installation

Via [NuGet](https://www.nuget.org/packages/NuExt.Minimal.Mvvm.AvalonDock):

```sh
dotnet add package NuExt.Minimal.Mvvm.AvalonDock
```

Or via Visual Studio:

1. Go to `Tools -> NuGet Package Manager -> Manage NuGet Packages for Solution...`.
2. Search for `NuExt.Minimal.Mvvm.AvalonDock`.
3. Click **Install**.

### Recommended companions

- [`NuExt.Minimal.Mvvm.SourceGenerator`](https://www.nuget.org/packages/NuExt.Minimal.Mvvm.SourceGenerator) removes ViewModel and command boilerplate at compile time.

## Usage

Attach the required services to an AvalonDock docking manager:

```xml
<avalonDock:DockingManager>
  <nx:Interaction.Behaviors>
    <nx:AvalonDockDocumentService x:Name="Documents" />
    <nx:AvalonDockToolWindowService x:Name="Tools" />
  </nx:Interaction.Behaviors>
</avalonDock:DockingManager>
```

The services are available to ViewModels as named services:

```csharp
public IAsyncDocumentManagerService DocumentManagerService =>
    GetRequiredService<IAsyncDocumentManagerService>("Documents");

public IAsyncToolWindowManagerService ToolWindowManagerService =>
    GetRequiredService<IAsyncToolWindowManagerService>("Tools");
```

### Tool windows

```csharp
IToolWindow toolWindow = await ToolWindowManagerService.CreateToolWindowAsync(
    toolWindowType: nameof(PropertiesView),
    viewModelFactory: static () => new PropertiesViewModel(),
    parentViewModel: this,
    parameter: null,
    options: new ToolWindowOptions
    {
        Id = "Properties",
        Title = "Properties",
        Placement = ToolWindowPlacement.Left,
        CanFloat = false,
        CanDockAsTabbedDocument = false
    },
    cancellationToken: cancellationToken);

toolWindow.Show();
```

Tool windows are long-lived UI panels. `Show()` displays and activates a panel, while `Hide()` hides or collapses it without disposing its ViewModel. The tool window manager owns created ViewModels and disposes them during service cleanup.

A tool window can be retrieved by its identifier:

```csharp
ToolWindowManagerService.FindToolWindowById("Properties")?.Show();
```

### ToggleDockingManager

`AvalonDockToolWindowService` supports `ToggleDockingManager`:

```xml
<avalonDock:ToggleDockingManager>
  <nx:Interaction.Behaviors>
    <nx:AvalonDockDocumentService x:Name="Documents" />
    <nx:AvalonDockToolWindowService x:Name="Tools" />
  </nx:Interaction.Behaviors>
</avalonDock:ToggleDockingManager>
```

Tool windows managed by `ToggleDockingManager` must be created before the manager is loaded. Their initial side is configured through `ToolWindowOptions.Placement`.

## Ecosystem

- [NuExt.Minimal.Mvvm](https://github.com/nu-ext/NuExt.Minimal.Mvvm)
- [NuExt.Minimal.Mvvm.SourceGenerator](https://github.com/nu-ext/NuExt.Minimal.Mvvm.SourceGenerator)
- [NuExt.Minimal.Behaviors.Wpf](https://github.com/nu-ext/NuExt.Minimal.Behaviors.Wpf)
- [NuExt.Presentation.Wpf](https://github.com/nu-ext/NuExt.Presentation.Wpf)
- [NuExt.Minimal.Mvvm.Wpf](https://github.com/nu-ext/NuExt.Minimal.Mvvm.Wpf)
- [NuExt.Minimal.Mvvm.MahApps.Metro](https://github.com/nu-ext/NuExt.Minimal.Mvvm.MahApps.Metro)
- [NuExt.System](https://github.com/nu-ext/NuExt.System)
- [NuExt.System.Data](https://github.com/nu-ext/NuExt.System.Data)
- [NuExt.System.Data.SQLite](https://github.com/nu-ext/NuExt.System.Data.SQLite)

## Contributing

Issues and PRs are welcome. Keep changes minimal and performance-conscious.

## License

MIT. See LICENSE.