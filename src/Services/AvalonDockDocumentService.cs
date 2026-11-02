using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Data;
using AvalonDock;
using AvalonDock.Layout;

namespace Minimal.Mvvm.Wpf;

/// <summary>
/// Manages documents within an AvalonDock docking manager.
/// Handles asynchronous document creation, activation, closing, and disposal.
/// </summary>
public sealed class AvalonDockDocumentService : DocumentServiceBase, IAsyncDocumentManagerService, IAsyncDisposable
{
    #region DockableDocument

    private sealed class DockableDocument : AsyncDisposable, IAsyncDocument, IViewModelProvider
    {
        private readonly AsyncLifetime _lifetime = new AsyncLifetime() { ContinueOnCapturedContext = true }
#if DEBUG
            .SetDebugInfo()
#endif
            ;

        private readonly CancellationTokenSource _cts = new();
        private LayoutDocumentPane? _hiddenDocumentPane;
        private int _hiddenDocumentIndex = -1;

        internal DockableDocument(AvalonDockDocumentService owner, LayoutDocument layoutDocument)
        {
            Owner = owner ?? throw new ArgumentNullException(nameof(owner));
            LayoutDocument = layoutDocument ?? throw new ArgumentNullException(nameof(layoutDocument));

            var viewModel = ViewModel;
            Debug.Assert(viewModel != null);

            _lifetime.AddDisposable(_cts);
            _lifetime.AddBracket(() => owner.AddDocument(this), () => owner.RemoveDocument(this));
            _lifetime.Add(RemoveFromLayout);//3) remove layout document
            _lifetime.Add(() => AvalonViewModelHelper.DetachContentFromContainer(LayoutDocument));//2) detach content
            _lifetime.AddAsync(DisposeViewModelAsync);//1) dispose vm
            _lifetime.AddBracket(() => SetDocument(LayoutDocument, this), () => SetDocument(LayoutDocument, null));

            if (viewModel != null && ViewModelHelper.ViewModelHasTitleProperty(viewModel))
            {
                _lifetime.AddBracket(
                    () => ViewModelHelper.SetViewTitleBinding(LayoutDocument.Content, LayoutContent.TitleProperty, LayoutDocument),
                    () => BindingOperations.ClearBinding(LayoutDocument, LayoutContent.TitleProperty));
            }

            var dpd = DependencyPropertyDescriptor.FromProperty(LayoutContent.TitleProperty, typeof(LayoutContent));
            if (dpd != null)
            {
                _lifetime.AddBracket(
                    () => dpd.AddValueChanged(LayoutDocument, OnLayoutDocumentTitleChanged),
                    () => dpd.RemoveValueChanged(LayoutDocument, OnLayoutDocumentTitleChanged));
            }
            Debug.Assert(dpd != null);

            if (LayoutDocument.Content is FrameworkElement view)
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

        public CancellationToken CancellationToken => _cts.Token;

        /// <inheritdoc/>
        public bool DisposeOnClose { get; set; }

        /// <inheritdoc/>
        public bool HideInsteadOfClose { get; set; }

        /// <inheritdoc/>
        public object Id { get; set; } = null!;

        /// <inheritdoc/>
        public string Title
        {
            get => LayoutDocument.Title ?? string.Empty;
            set => LayoutDocument.Title = value;
        }

        /// <inheritdoc/>
        public object? ViewModel => ViewModelHelper.GetViewModelFromView(LayoutDocument.Content);

        private bool IsHidden => _hiddenDocumentIndex != -1;

        public bool IsClosing { get; private set; }

        private AvalonDockDocumentService Owner { get; }

        internal LayoutDocument LayoutDocument { get; }

        #endregion

        #region Event Handlers

        private void OnLayoutDocumentTitleChanged(object? sender, EventArgs e)
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
        public ValueTask CloseAsync(bool force)
        {
            if (IsDisposingOrDisposed)
            {
                return ValueTask.CompletedTask;
            }

            var dispatcher = LayoutDocument.Dispatcher;
            if (dispatcher.CheckAccess())
            {
                return CloseAsyncCore(force);
            }
            return new ValueTask(dispatcher.InvokeAsync(async () => await CloseAsyncCore(force)).Task.Unwrap());
        }

        private async ValueTask CloseAsyncCore(bool force)
        {
            Debug.Assert(LayoutDocument.CheckAccess());
            Debug.Assert(IsDisposed == false);

            VerifyAccess();
            CheckDisposed();

            if (IsClosing)
            {
                return;
            }

            IsClosing = true;
            try
            {
                if (force)
                {
#if NET8_0_OR_GREATER
                    await _cts.CancelAsync();
#else
                    _cts.Cancel();
#endif
                }
                else
                {
                    if (await CanCloseAsync(LayoutDocument, CancellationToken) == false)
                    {
                        return;
                    }
                }

                await DisposeAsync().ConfigureAwait(false);
            }
            finally
            {
                IsClosing = false;
            }
        }

        /// <inheritdoc/>
        public void Hide()
        {
            CheckDisposed();
            VerifyAccess();

            Debug.Assert(LayoutDocument.Parent is LayoutDocumentPane);
            if (LayoutDocument.Parent is not LayoutDocumentPane pane)
                return;

            _hiddenDocumentPane = pane;
            _hiddenDocumentIndex = pane.IndexOf(LayoutDocument);

            LayoutDocument.IsSelected = false;
            LayoutDocument.IsActive = false;
            pane.Children.Remove(LayoutDocument);
        }

        /// <inheritdoc/>
        public void Show()
        {
            CheckDisposed();
            VerifyAccess();

            if (LayoutDocument.Parent is null)
            {
                Debug.Assert(IsHidden);
                LayoutDocumentPane? pane = _hiddenDocumentPane;

                if (pane?.Root is null || !ReferenceEquals(pane.Root, Owner.AssociatedObject?.Layout))
                    pane = Owner.AssociatedObject?.GetLayoutDocumentPane();

                Throw.InvalidOperationExceptionIf(pane is null, "The layout must contain at least one LayoutDocumentPane to host documents.");

                int index = Math.Clamp(_hiddenDocumentIndex, 0, pane.Children.Count);
                pane.Children.Insert(index, LayoutDocument);

                _hiddenDocumentPane = null;
                _hiddenDocumentIndex = -1;
            }

            LayoutDocument.IsSelected = true;
            LayoutDocument.IsActive = true;
        }

        protected override async ValueTask DisposeAsyncCore()
        {
            Debug.Assert(ContinueOnCapturedContext);
            Debug.Assert(CheckAccess());
            VerifyAccess();
#if NET8_0_OR_GREATER
            await _cts.CancelAsync();
#else
            _cts.Cancel();
#endif
            //Hide(); do not remove from layout here
            await _lifetime.DisposeAsync();
        }

        private ValueTask DisposeViewModelAsync()
        {
            var viewModel = ViewModel;
            Debug.Assert(viewModel != null);

            if (DisposeOnClose && viewModel is IAsyncDisposable asyncDisposable)
            {
                return asyncDisposable.DisposeAsync();
            }
            return ValueTask.CompletedTask;
        }

        private void RemoveFromLayout()
        {
            if (LayoutDocument.Parent is {} container)
                container.RemoveChild(LayoutDocument);

            _hiddenDocumentPane = null;
            _hiddenDocumentIndex = -1;
        }

        #endregion
    }

    #endregion

    private readonly ObservableCollection<DockableDocument> _documents = [];

    private bool _isActiveDocumentChanging;

    private IDisposable? _subscription;

    public AvalonDockDocumentService()
    {
        if (ViewModelHelper.IsInDesignMode) return;

        (_documents as INotifyPropertyChanged).PropertyChanged += OnDocumentsPropertyChanged;
    }

    #region Properties

    private new DockingManager? AssociatedObject => (DockingManager?)base.AssociatedObject;

    /// <inheritdoc/>
    public int Count => _documents.Count;

    /// <inheritdoc/>
    public IEnumerable<IAsyncDocument> Documents => _documents;

    #endregion

    #region Event Handlers

    protected override void OnActiveDocumentChanged(IAsyncDocument? oldValue, IAsyncDocument? newValue)
    {
        Debug.Assert(oldValue != newValue);
        VerifyAccess();

        if (!_isActiveDocumentChanging && newValue != null)
        {
            _isActiveDocumentChanging = true;
            try
            {
                newValue.Show();
            }
            finally
            {
                _isActiveDocumentChanging = false;
            }
        }

        base.OnActiveDocumentChanged(oldValue, newValue);
    }

    private void OnDockingManagerActiveContentChanged(object? sender, EventArgs e)
    {
        if (_isActiveDocumentChanging)
        {
            return;
        }

        Debug.Assert(Equals(sender, AssociatedObject));
        if (sender is not DockingManager manager)
        {
            return;
        }

        IAsyncDocument? document;

        if (manager.Layout.ActiveContent is LayoutDocument layoutDocument)
        {
            document = GetDocument(layoutDocument);
        }
        else if (manager.Layout.ActiveContent is null)
        {
            document = null;
        }
        else
        {
            // Keep the last active document when a tool window becomes active.
            return;
        }

        _isActiveDocumentChanging = true;
        try
        {
            ActiveDocument = document;
        }
        finally
        {
            _isActiveDocumentChanging = false;
        }
    }

    private void OnDockingManagerDocumentClosing(object? sender, DocumentClosingEventArgs e)
    {
        Debug.Assert(Equals(sender, AssociatedObject));
        if (sender is not DockingManager manager)
        {
            return;
        }

        if (e.Cancel || GetDocument(e.Document) is not DockableDocument document || document.IsClosing || document.CancellationToken.IsCancellationRequested)
        {
            return;
        }

        e.Cancel = true;
        _ = manager.Dispatcher.InvokeAsync(async () => await CloseLayoutDocumentAsync(document, document.CancellationToken).ConfigureAwait(false));
    }

    private static async ValueTask CloseLayoutDocumentAsync(DockableDocument document, CancellationToken cancellationToken)
    {
        Debug.Assert(!document.IsClosing);

        if (document.HideInsteadOfClose)
        {
            if (await CanCloseAsync(document.LayoutDocument, cancellationToken) == false)
            {
                return;
            }
            document.Hide();
            return;
        }

        await document.CloseAsync(false);
    }

    private void OnDocumentsPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(_documents.Count))
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

    private void AddDocument(DockableDocument document)
    {
        VerifyAccess();
        _documents.Add(document);
    }

    private void RemoveDocument(DockableDocument document)
    {
        VerifyAccess();
        _documents.Remove(document);

        if (!ReferenceEquals(ActiveDocument, document))
        {
            return;
        }

        ActiveDocument = AssociatedObject?.Layout.ActiveContent is LayoutDocument layoutDocument ? GetDocument(layoutDocument) : null;
    }

    private static async ValueTask<bool> CanCloseAsync(LayoutDocument layoutDocument, CancellationToken cancellationToken)
    {
        try
        {
            var viewModel = ViewModelHelper.GetViewModelFromView(layoutDocument.Content);
            Debug.Assert(viewModel != null);
            switch (viewModel)
            {
                case IAsyncDocumentContent documentContent when await documentContent.CanCloseAsync(cancellationToken) == false:
                    cancellationToken.ThrowIfCancellationRequested();
                    return false;
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            //do nothing and return
            return false;
        }
        return true;
    }

    /// <inheritdoc/>
    public async ValueTask<IAsyncDocument> CreateDocumentAsync(string? documentType, object? viewModel,
        object? parentViewModel, object? parameter, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(AssociatedObject);
        cancellationToken.ThrowIfCancellationRequested();

        var layoutDocumentPane = AssociatedObject.GetLayoutDocumentPane();
        Throw.InvalidOperationExceptionIf(layoutDocumentPane is null, "The layout must contain at least one LayoutDocumentPane to host documents.");

        var view = await CreateViewAsync(documentType, cancellationToken);

        var layoutDocument = CreateLayoutDocument();
        layoutDocument.Content = view;
        // LayoutDocument does not have a DataContext property, so nothing to bind here.

        await ViewModelHelper.InitializeViewAsync(view, viewModel, parentViewModel, parameter, cancellationToken);

        var document = new DockableDocument(this, layoutDocument) { ContinueOnCapturedContext = true };
        try
        {
            layoutDocumentPane.Children.Add(layoutDocument);
        }
        catch
        {
            await document.DisposeAsync().ConfigureAwait(false);
            throw;
        }
        return document;
    }

    private static LayoutDocument CreateLayoutDocument()
    {
        var layoutDocument = new LayoutDocument
        {
            Title = "Document"
        };
        return layoutDocument;
    }

    /// <inheritdoc/>
    public async ValueTask CloseAllAsync()
    {
        if (_documents.Count == 0)
        {
            return;
        }
        await Task.WhenAll(_documents.ToList().Select(static async x => await x.CloseAsync(force: true).ConfigureAwait(false))).ConfigureAwait(false);
        if (_documents.Count == 0)
        {
            return;
        }

        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        void OnDocumentsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (_documents.Count == 0)
            {
                tcs.TrySetResult(true);
            }
        }

        _documents.CollectionChanged += OnDocumentsCollectionChanged;
        try
        {
            if (_documents.Count == 0)
            {
                tcs.TrySetResult(true);
            }
            await tcs.Task.ConfigureAwait(false);
        }
        finally
        {
            _documents.CollectionChanged -= OnDocumentsCollectionChanged;
        }
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        try
        {
            Disposable.DisposeAndNull(ref _subscription);
            await CloseAllAsync().ConfigureAwait(false);
        }
        finally
        {
            (_documents as INotifyPropertyChanged).PropertyChanged -= OnDocumentsPropertyChanged;
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

        if (manager.DocumentsSource is not null)
        {
            Throw.InvalidOperationException("Can't use not null DocumentsSource in this service.");
        }

        var lifetime = new Lifetime()
#if DEBUG
            .SetDebugInfo()
#endif
            ;

        lifetime.AddBracket(
            () => manager.ActiveContentChanged += OnDockingManagerActiveContentChanged,
            () => manager.ActiveContentChanged -= OnDockingManagerActiveContentChanged);

        lifetime.AddBracket(
            () => manager.DocumentClosing += OnDockingManagerDocumentClosing,
            () => manager.DocumentClosing -= OnDockingManagerDocumentClosing);

        return lifetime;
    }

    #endregion
}

internal static partial class EventArgsCache
{
    internal static readonly PropertyChangedEventArgs CountPropertyChanged = new(nameof(IAsyncDocumentManagerService.Count));
    internal static readonly PropertyChangedEventArgs TitlePropertyChanged = new(nameof(IAsyncDocument.Title));
    internal static readonly PropertyChangedEventArgs ViewModelPropertyChanged = new(nameof(IViewModelProvider.ViewModel));
}