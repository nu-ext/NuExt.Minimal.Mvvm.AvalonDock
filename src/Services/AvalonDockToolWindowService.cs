using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Data;
using AvalonDock;
using AvalonDock.Controls;
using AvalonDock.Layout;

namespace Minimal.Mvvm.Wpf;

public sealed class AvalonDockToolWindowService : ViewServiceBase, IAsyncToolWindowManagerService, IAsyncDisposable
{
    #region ToolWindow

    private sealed class ToolWindow : AsyncDisposable, IToolWindow, IViewModelProvider
    {
        private readonly AsyncLifetime _lifetime = new AsyncLifetime() { ContinueOnCapturedContext = true }
#if DEBUG
            .SetDebugInfo()
#endif
            ;

        internal ToolWindow(AvalonDockToolWindowService owner, LayoutAnchorable layoutAnchorable)
        {
            Owner = owner ?? throw new ArgumentNullException(nameof(owner));
            LayoutAnchorable = layoutAnchorable ?? throw new ArgumentNullException(nameof(layoutAnchorable));

            var viewModel = ViewModel;
            Debug.Assert(viewModel != null);

            _lifetime.AddBracket(() => owner.AddToolWindow(this), () => owner.RemoveToolWindow(this));
            _lifetime.Add(RemoveFromLayout);//3) remove layout anchorable
            _lifetime.Add(() => AvalonViewModelHelper.DetachContentFromContainer(LayoutAnchorable));//2) detach content
            _lifetime.AddAsync(DisposeViewModelAsync);//1) dispose vm
            _lifetime.AddBracket(() => SetToolWindow(LayoutAnchorable, this), () => SetToolWindow(LayoutAnchorable, null));

            if (viewModel != null && ViewModelHelper.ViewModelHasTitleProperty(viewModel))
            {
                _lifetime.AddBracket(
                    () => ViewModelHelper.SetViewTitleBinding(LayoutAnchorable.Content, LayoutContent.TitleProperty, LayoutAnchorable),
                    () => BindingOperations.ClearBinding(LayoutAnchorable, LayoutContent.TitleProperty));
            }

            var dpd = DependencyPropertyDescriptor.FromProperty(LayoutContent.TitleProperty, typeof(LayoutContent));
            if (dpd != null)
            {
                _lifetime.AddBracket(
                    () => dpd.AddValueChanged(LayoutAnchorable, OnLayoutAnchorableTitleChanged),
                    () => dpd.RemoveValueChanged(LayoutAnchorable, OnLayoutAnchorableTitleChanged));
            }
            Debug.Assert(dpd != null);

            if (LayoutAnchorable.Content is FrameworkElement view)
            {
                var viewModelPropertyDescriptor = DependencyPropertyDescriptor.FromProperty(FrameworkElement.DataContextProperty, typeof(FrameworkElement));

                if (viewModelPropertyDescriptor != null)
                {
                    _lifetime.AddBracket(
                        () => viewModelPropertyDescriptor.AddValueChanged(view, OnViewModelChanged),
                        () => viewModelPropertyDescriptor.RemoveValueChanged(view, OnViewModelChanged));
                }
                Debug.Assert(viewModelPropertyDescriptor != null);
            }
        }

        #region Properties

        /// <inheritdoc/>
        public bool CanAutoHide
        {
            get => LayoutAnchorable.CanAutoHide;
            set => LayoutAnchorable.CanAutoHide = value;
        }

        /// <inheritdoc/>
        public bool CanDockAsTabbedDocument
        {
            get => LayoutAnchorable.CanDockAsTabbedDocument;
            set => LayoutAnchorable.CanDockAsTabbedDocument = value;
        }

        /// <inheritdoc/>
        public bool CanFloat
        {
            get => LayoutAnchorable.CanFloat;
            set => LayoutAnchorable.CanFloat = value;
        }

        /// <inheritdoc/>
        public object Id { get; set; } = null!;

        /// <inheritdoc/>
        public string Title
        {
            get => LayoutAnchorable.Title ?? string.Empty;
            set => LayoutAnchorable.Title = value;
        }

        /// <inheritdoc/>
        public object? ViewModel => ViewModelHelper.GetViewModelFromView(LayoutAnchorable.Content);

        private AvalonDockToolWindowService Owner { get; }

        private LayoutAnchorable LayoutAnchorable { get; }

        #endregion

        #region Event Handlers

        private void OnLayoutAnchorableTitleChanged(object? sender, EventArgs e)
        {
            OnPropertyChanged(EventArgsCache.TitlePropertyChanged);
        }

        private void OnViewModelChanged(object? sender, EventArgs e)
        {
            OnPropertyChanged(EventArgsCache.ViewModelPropertyChanged);
        }

        #endregion

        #region Methods

        /// <inheritdoc/>
        public void Hide()
        {
            CheckDisposed();
            VerifyAccess();

            if (Owner.AssociatedObject is ToggleDockingManager toggleDockingManager)
            {
                if (!LayoutAnchorable.IsAutoHidden)
                {
                    var zone = toggleDockingManager.GetCurrentAnchorableZone(LayoutAnchorable);
                    toggleDockingManager.ToggleAnchorable(LayoutAnchorable, zone);
                }
                return;
            }

            if (LayoutAnchorable.IsVisible)
            {
                LayoutAnchorable.Hide();
            }
        }

        /// <inheritdoc/>
        public void Show()
        {
            CheckDisposed();
            VerifyAccess();

            if (Owner.AssociatedObject is ToggleDockingManager toggleDockingManager)
            {
                if (LayoutAnchorable.IsAutoHidden)
                {
                    var zone = toggleDockingManager.GetCurrentAnchorableZone(LayoutAnchorable);
                    toggleDockingManager.ToggleAnchorable(LayoutAnchorable, zone);
                }
                else
                {
                    LayoutAnchorable.IsSelected = true;
                    LayoutAnchorable.IsActive = true;
                }
                return;
            }

            if (LayoutAnchorable.IsHidden)
            {
                LayoutAnchorable.Show();
            }

            LayoutAnchorable.IsSelected = true;
            LayoutAnchorable.IsActive = true;
        }

        protected override ValueTask DisposeAsyncCore()
        {
            Debug.Assert(ContinueOnCapturedContext);
            Debug.Assert(CheckAccess());
            VerifyAccess();

            return _lifetime.DisposeAsync();
        }

        private ValueTask DisposeViewModelAsync()
        {
            var viewModel = ViewModel;
            Debug.Assert(viewModel != null);

            if (viewModel is IAsyncDisposable asyncDisposable)
            {
                return asyncDisposable.DisposeAsync();
            }
            return ValueTask.CompletedTask;
        }

        private void RemoveFromLayout()
        {
            if (Owner.AssociatedObject is ToggleDockingManager toggleDockingManager)
            {
                toggleDockingManager.RemoveAnchorableButton(LayoutAnchorable);
            }

            if (LayoutAnchorable.Parent is { } container)
            {
                container.RemoveChild(LayoutAnchorable);
            }
        }

        #endregion
    }

    #endregion

    private readonly ObservableCollection<ToolWindow> _toolWindows = [];

    private bool _isActiveToolWindowChanging;

    private IDisposable? _subscription;

    public AvalonDockToolWindowService()
    {
        if (ViewModelHelper.IsInDesignMode) return;

        (_toolWindows as INotifyPropertyChanged).PropertyChanged += OnToolWindowsPropertyChanged;
    }

    #region Properties

    public IToolWindow? ActiveToolWindow
    {
        get => (IToolWindow?)GetValue(ActiveToolWindowProperty);
        set => SetValue(ActiveToolWindowProperty, value);
    }

    private new DockingManager? AssociatedObject => (DockingManager?)base.AssociatedObject;

    /// <inheritdoc/>
    public int Count => _toolWindows.Count;

    /// <inheritdoc/>
    public IEnumerable<IToolWindow> ToolWindows => _toolWindows;

    #endregion

    #region Events

    /// <inheritdoc/>
    public event EventHandler<ActiveToolWindowChangedEventArgs>? ActiveToolWindowChanged;

    #endregion

    #region Event Handlers

    private void OnActiveToolWindowChanged(IToolWindow? oldValue, IToolWindow? newValue)
    {
        Debug.Assert(oldValue != newValue);
        VerifyAccess();

        if (!_isActiveToolWindowChanging && newValue is not null)
        {
            _isActiveToolWindowChanging = true;
            try
            {
                newValue.Show();
            }
            finally
            {
                _isActiveToolWindowChanging = false;
            }
        }

        ActiveToolWindowChanged?.Invoke(this, new ActiveToolWindowChangedEventArgs(oldValue, newValue));
    }

    private void OnDockingManagerActiveContentChanged(object? sender, EventArgs e)
    {
        if (_isActiveToolWindowChanging)
        {
            return;
        }

        Debug.Assert(Equals(sender, AssociatedObject));
        if (sender is not DockingManager manager)
        {
            return;
        }

        IToolWindow? toolWindow;

        if (manager.Layout.ActiveContent is LayoutAnchorable layoutAnchorable)
        {
            toolWindow = GetToolWindow(layoutAnchorable);
        }
        else if (manager.Layout.ActiveContent is null)
        {
            toolWindow = null;
        }
        else
        {
            // Keep the last active tool window when a document becomes active.
            return;
        }

        _isActiveToolWindowChanging = true;
        try
        {
            ActiveToolWindow = toolWindow;
        }
        finally
        {
            _isActiveToolWindowChanging = false;
        }
    }

    private void OnToolWindowsPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(_toolWindows.Count))
        {
            OnPropertyChanged(EventArgsCache.CountPropertyChanged);
        }
    }

    private void OnDockingManagerLoaded(object sender, RoutedEventArgs e)
    {
        Debug.Assert(Equals(sender, AssociatedObject));

        if (sender is FrameworkElement element)
        {
            element.Loaded -= OnDockingManagerLoaded;
        }

        Debug.Assert(_subscription is null);
        Disposable.DisposeAndNull(ref _subscription);
        _subscription = SubscribeDockingManager(AssociatedObject);
    }

    #endregion

    #region Methods

    private void AddToolWindow(ToolWindow toolWindow)
    {
        VerifyAccess();
        _toolWindows.Add(toolWindow);
    }

    private void RemoveToolWindow(ToolWindow toolWindow)
    {
        VerifyAccess();
        _toolWindows.Remove(toolWindow);

        if (!ReferenceEquals(ActiveToolWindow, toolWindow))
        {
            return;
        }

        ActiveToolWindow = AssociatedObject?.Layout.ActiveContent is LayoutAnchorable layoutAnchorable ? GetToolWindow(layoutAnchorable) : null;
    }

    /// <inheritdoc/>
    public async ValueTask<IToolWindow> CreateToolWindowAsync(string? toolWindowType, object? viewModel,
        object? parentViewModel, object? parameter, ToolWindowOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(AssociatedObject);
        ArgumentNullException.ThrowIfNull(options);
        cancellationToken.ThrowIfCancellationRequested();

        var view = await CreateViewAsync(toolWindowType, cancellationToken);

        var layoutAnchorable = CreateLayoutAnchorable(options);
        layoutAnchorable.Content = view;
        // LayoutAnchorable does not have a DataContext property, so nothing to bind here.

        await ViewModelHelper.InitializeViewAsync(view, viewModel, parentViewModel, parameter, cancellationToken);

        var toolWindow = new ToolWindow(this, layoutAnchorable)
        {
            Id = options.Id,
            ContinueOnCapturedContext = true
        };

        try
        {
            AddToLayout(layoutAnchorable, options);
        }
        catch
        {
            await toolWindow.DisposeAsync().ConfigureAwait(false);
            throw;
        }

        return toolWindow;
    }

    private void AddToLayout(LayoutAnchorable layoutAnchorable, ToolWindowOptions options)
    {
        if (AssociatedObject is ToggleDockingManager toggleDockingManager)
        {
            toggleDockingManager.AddAnchorable(layoutAnchorable, options.Placement);
            return;
        }

        var pane = string.IsNullOrEmpty(options.PaneName)
            ? AssociatedObject!.GetLayoutAnchorablePane()
            : AssociatedObject!.GetLayoutAnchorablePane(options.PaneName!);

        Throw.InvalidOperationExceptionIf(pane is null,
            string.IsNullOrEmpty(options.PaneName)
                ? "The layout must contain at least one LayoutAnchorablePane to host tool windows."
                : $"The layout does not contain a LayoutAnchorablePane named '{options.PaneName}'.");

        pane.Children.Add(layoutAnchorable);
    }

    private static LayoutAnchorable CreateLayoutAnchorable(ToolWindowOptions options)
    {
        var layoutAnchorable = new LayoutAnchorable
        {
            ContentId = options.Id.ToString(),
            Title = options.Title ?? string.Empty,
            CanClose = false,
            CanFloat = options.CanFloat,
            CanHide = true,//Workaround for a ToggleDockingManager bug: disabling hiding can remove the toggle button
                           //without adding the anchorable to the hidden collection, making it impossible to restore.
            CanAutoHide = options.CanAutoHide,
            CanDockAsTabbedDocument = options.CanDockAsTabbedDocument
        };

        if (options.Icon is not null)
        {
            ToggleDock.SetIcon(layoutAnchorable, options.Icon);
        }

        return layoutAnchorable;
    }

    /// <inheritdoc />
    public void HideAll()
    {
        VerifyAccess();

        foreach (var toolWindow in _toolWindows)
        {
            toolWindow.Hide();
        }
    }

    private Task DisposeAllAsync()
    {
        if (_toolWindows.Count == 0)
        {
            return Task.CompletedTask;
        }

        return Task.WhenAll(_toolWindows.ToList().Select(static x => x.DisposeAsync().AsTask()));
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        try
        {
            Disposable.DisposeAndNull(ref _subscription);
            await DisposeAllAsync().ConfigureAwait(false);
        }
        finally
        {
            (_toolWindows as INotifyPropertyChanged).PropertyChanged -= OnToolWindowsPropertyChanged;
        }
    }

    /// <inheritdoc />
    protected override void OnAttached()
    {
        base.OnAttached();
        Debug.Assert(_subscription is null);

        if (AssociatedObject!.IsLoaded)
        {
            Disposable.DisposeAndNull(ref _subscription);
            _subscription = SubscribeDockingManager(AssociatedObject);
        }
        else
        {
            AssociatedObject.Loaded += OnDockingManagerLoaded;
        }
    }

    /// <inheritdoc />
    protected override void OnDetaching()
    {
        AssociatedObject!.Loaded -= OnDockingManagerLoaded;
        Disposable.DisposeAndNull(ref _subscription);

        base.OnDetaching();
    }

    private Lifetime? SubscribeDockingManager(DockingManager? manager)
    {
        if (manager is null)
        {
            return null;
        }

        var lifetime = new Lifetime()
#if DEBUG
            .SetDebugInfo()
#endif
            ;

        lifetime.AddBracket(
            () => manager.ActiveContentChanged += OnDockingManagerActiveContentChanged,
            () => manager.ActiveContentChanged -= OnDockingManagerActiveContentChanged);

        return lifetime;
    }

    #endregion

    #region Dependency Properties

    public static readonly DependencyProperty ActiveToolWindowProperty = DependencyProperty.Register(
        nameof(ActiveToolWindow), typeof(IToolWindow), typeof(AvalonDockToolWindowService),
        new FrameworkPropertyMetadata(null,
            FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
            static (d, e) => ((AvalonDockToolWindowService)d).OnActiveToolWindowChanged(e.OldValue as IToolWindow, e.NewValue as IToolWindow)));

    public static readonly DependencyProperty ToolWindowProperty = DependencyProperty.RegisterAttached(
        "ToolWindow", typeof(IToolWindow), typeof(AvalonDockToolWindowService));

    #endregion

    #region Dependency Methods

    public static IToolWindow? GetToolWindow(DependencyObject element)
    {
        ArgumentNullException.ThrowIfNull(element);
        return (IToolWindow?)element.GetValue(ToolWindowProperty);
    }

    public static void SetToolWindow(DependencyObject element, IToolWindow? value)
    {
        ArgumentNullException.ThrowIfNull(element);
        element.SetValue(ToolWindowProperty, value);
    }

    #endregion
}