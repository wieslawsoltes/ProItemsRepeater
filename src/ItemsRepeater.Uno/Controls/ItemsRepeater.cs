using System;
using System.Collections;
using System.Collections.Specialized;
using Avalonia.LogicalTree;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Logging;
using Avalonia.Metadata;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Markup;
using AttachedLayout = Avalonia.Layout.AttachedLayout;
using NonVirtualizingLayout = Avalonia.Layout.NonVirtualizingLayout;
using StackLayout = Avalonia.Layout.StackLayout;
using VirtualizingLayout = Avalonia.Layout.VirtualizingLayout;

namespace Avalonia.Controls;

[ContentProperty(Name = nameof(ItemTemplate))]
public partial class ItemsRepeater : Panel, IChildIndexProvider
{
    public static readonly DependencyProperty HorizontalCacheLengthProperty =
        DependencyProperty.Register(
            nameof(HorizontalCacheLength),
            typeof(double),
            typeof(ItemsRepeater),
            new PropertyMetadata(2d, OnDependencyPropertyChanged));

    public static readonly DependencyProperty ItemTemplateProperty =
        DependencyProperty.Register(
            nameof(ItemTemplate),
            typeof(object),
            typeof(ItemsRepeater),
            new PropertyMetadata(null, OnDependencyPropertyChanged));

    public static readonly DependencyProperty ItemsSourceProperty =
        DependencyProperty.Register(
            nameof(ItemsSource),
            typeof(IEnumerable),
            typeof(ItemsRepeater),
            new PropertyMetadata(null, OnDependencyPropertyChanged));

    public static readonly DependencyProperty LayoutProperty =
        DependencyProperty.Register(
            nameof(Layout),
            typeof(AttachedLayout),
            typeof(ItemsRepeater),
            new PropertyMetadata(null, OnDependencyPropertyChanged));

    public static readonly DependencyProperty VerticalCacheLengthProperty =
        DependencyProperty.Register(
            nameof(VerticalCacheLength),
            typeof(double),
            typeof(ItemsRepeater),
            new PropertyMetadata(2d, OnDependencyPropertyChanged));

    public static readonly DependencyProperty IsLogicalScrollEnabledProperty =
        DependencyProperty.Register(
            nameof(IsLogicalScrollEnabled),
            typeof(bool),
            typeof(ItemsRepeater),
            new PropertyMetadata(true, OnDependencyPropertyChanged));

    private static readonly DependencyProperty VirtualizationInfoProperty =
        DependencyProperty.RegisterAttached(
            "VirtualizationInfo",
            typeof(VirtualizationInfo),
            typeof(ItemsRepeater),
            new PropertyMetadata(null));

    internal static readonly Rect InvalidRect = new(-1, -1, -1, -1);
    internal static readonly Point ClearedElementsArrangePosition = new(-10000.0, -10000.0);

    private readonly ViewManager _viewManager;
    private readonly ViewportManager _viewportManager;
    private RepeaterLayoutContext? _layoutContext;
    private EventHandler<ChildIndexChangedEventArgs>? _childIndexChanged;
    private bool _isLayoutInProgress;
    private NotifyCollectionChangedEventArgs? _processingItemsSourceChange;
    private ItemsRepeaterElementPreparedEventArgs? _elementPreparedArgs;
    private ItemsRepeaterElementClearingEventArgs? _elementClearingArgs;
    private ItemsRepeaterElementIndexChangedEventArgs? _elementIndexChangedArgs;

    public ItemsRepeater()
    {
        _viewManager = new ViewManager(this);
        _viewportManager = new ViewportManager(this);
        TabFocusNavigation = KeyboardNavigationMode.Once;

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;

        if (Layout is null)
        {
            Layout = new StackLayout();
        }
    }

    [InheritDataTypeFromItems(nameof(ItemsSource))]
    public object? ItemTemplate
    {
        get => GetValue(ItemTemplateProperty);
        set => SetValue(ItemTemplateProperty, value);
    }

    public IEnumerable? ItemsSource
    {
        get => (IEnumerable?)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public AttachedLayout? Layout
    {
        get => (AttachedLayout?)GetValue(LayoutProperty);
        set => SetValue(LayoutProperty, value);
    }

    public double HorizontalCacheLength
    {
        get => (double)GetValue(HorizontalCacheLengthProperty);
        set => SetValue(HorizontalCacheLengthProperty, value);
    }

    public double VerticalCacheLength
    {
        get => (double)GetValue(VerticalCacheLengthProperty);
        set => SetValue(VerticalCacheLengthProperty, value);
    }

    public bool IsLogicalScrollEnabled
    {
        get => (bool)GetValue(IsLogicalScrollEnabledProperty);
        set => SetValue(IsLogicalScrollEnabledProperty, value);
    }

    public Microsoft.UI.Xaml.Controls.ItemsSourceView? ItemsSourceView { get; private set; }

    internal IElementFactory? ItemTemplateShim { get; private set; }
    internal Point LayoutOrigin { get; set; }
    internal object? LayoutState { get; set; }
    internal FrameworkElement? MadeAnchor => _viewportManager.MadeAnchor;
    internal Rect RealizationWindow => _viewportManager.GetLayoutRealizationWindow();
    internal FrameworkElement? SuggestedAnchor => _viewportManager.SuggestedAnchor;
    private bool IsProcessingCollectionChange => _processingItemsSourceChange is not null;
    private RepeaterLayoutContext LayoutContext => _layoutContext ??= new RepeaterLayoutContext(this);

    public event EventHandler<ItemsRepeaterElementClearingEventArgs>? ElementClearing;
    public event EventHandler<ItemsRepeaterElementIndexChangedEventArgs>? ElementIndexChanged;
    public event EventHandler<ItemsRepeaterElementPreparedEventArgs>? ElementPrepared;

    event EventHandler<ChildIndexChangedEventArgs>? IChildIndexProvider.ChildIndexChanged
    {
        add => _childIndexChanged += value;
        remove => _childIndexChanged -= value;
    }

    public int GetElementIndex(UIElement element) => GetElementIndexImpl(element);

    public UIElement? TryGetElement(int index) => GetElementFromIndexImpl(index);

    public UIElement GetOrCreateElement(int index) => GetOrCreateElementImpl(index);

    int IChildIndexProvider.GetChildIndex(UIElement child)
    {
        return child is FrameworkElement element
            ? GetElementIndex(element)
            : -1;
    }

    bool IChildIndexProvider.TryGetTotalCount(out int count)
    {
        count = ItemsSourceView?.Count ?? 0;
        return true;
    }

    internal void PinElement(FrameworkElement element) => _viewManager.UpdatePin(element, true);

    internal void UnpinElement(FrameworkElement element) => _viewManager.UpdatePin(element, false);

    internal static VirtualizationInfo? TryGetVirtualizationInfo(UIElement? element) =>
        element is null ? null : (VirtualizationInfo?)element.GetValue(VirtualizationInfoProperty);

    internal static VirtualizationInfo GetVirtualizationInfo(UIElement element)
    {
        var result = TryGetVirtualizationInfo(element);
        if (result is null)
        {
            result = new VirtualizationInfo();
            element.SetValue(VirtualizationInfoProperty, result);
        }

        return result;
    }

    protected override Windows.Foundation.Size MeasureOverride(Windows.Foundation.Size availableSize)
    {
        var available = availableSize.ToAvalonia();

        if (_isLayoutInProgress)
        {
            throw new InvalidOperationException("Reentrancy detected during layout.");
        }

        if (IsProcessingCollectionChange)
        {
            throw new InvalidOperationException("Cannot run layout in the middle of a collection change.");
        }

        _viewportManager.OnOwnerMeasuring();
        SetViewport(available, raiseInvalidated: true, invalidateMeasure: false);

        var layout = Layout;
        var layoutId = GetLayoutId();
        var itemCount = ItemsSourceView?.Count ?? 0;
        var realizationWindow = _viewportManager.GetLayoutRealizationWindow();
        var visibleWindow = _viewportManager.GetLayoutVisibleWindow();
        using var activity = ItemsRepeaterDiagnostics.StartMeasure(layoutId, itemCount, available, realizationWindow, visibleWindow);
        var measureTimestamp = ItemsRepeaterDiagnostics.GetTimestamp();
        var measureCompleted = false;

        _isLayoutInProgress = true;

        try
        {
            _viewManager.PrunePinnedElements();

            var extent = new Rect();
            var desiredSize = new Size();

            if (layout != null)
            {
                desiredSize = MeasureLayout(layout, LayoutContext, available);
                extent = new Rect(LayoutOrigin.X, LayoutOrigin.Y, desiredSize.Width, desiredSize.Height);

                for (var i = 0; i < Children.Count; ++i)
                {
                    if (Children[i] is not FrameworkElement element)
                    {
                        continue;
                    }

                    var virtInfo = GetVirtualizationInfo(element);
                    if (virtInfo.Owner == ElementOwner.Layout &&
                        virtInfo.AutoRecycleCandidate &&
                        !virtInfo.KeepAlive)
                    {
                        Logger.TryGet(LogEventLevel.Verbose, "Repeater")?.Log(this, "AutoClear - {Index}", virtInfo.Index);
                        ClearElementImpl(element);
                    }
                }
            }

            SetExtent(desiredSize);
            _viewportManager.SetLayoutExtent(extent);
            measureCompleted = true;
            return desiredSize.ToNative();
        }
        finally
        {
            _isLayoutInProgress = false;
            if (measureCompleted)
            {
                ItemsRepeaterDiagnostics.RecordMeasure(
                    ItemsRepeaterDiagnostics.GetElapsedMilliseconds(measureTimestamp),
                    layoutId,
                    itemCount,
                    GetRealizedCountForDiagnostics(layout));
            }
        }
    }

    protected override Windows.Foundation.Size ArrangeOverride(Windows.Foundation.Size finalSize)
    {
        var final = finalSize.ToAvalonia();

        if (_isLayoutInProgress)
        {
            throw new InvalidOperationException("Reentrancy detected during layout.");
        }

        if (IsProcessingCollectionChange)
        {
            throw new InvalidOperationException("Cannot run layout in the middle of a collection change.");
        }

        var layout = Layout;
        var layoutId = GetLayoutId();
        var itemCount = ItemsSourceView?.Count ?? 0;
        using var activity = ItemsRepeaterDiagnostics.StartArrange(layoutId, itemCount, final);
        var arrangeTimestamp = ItemsRepeaterDiagnostics.GetTimestamp();
        var arrangeCompleted = false;

        _isLayoutInProgress = true;

        try
        {
            var arrangeSize = layout is null ? final : ArrangeLayout(layout, LayoutContext, final);
            SetViewport(final, raiseInvalidated: true, invalidateMeasure: false, forceInvalidate: true);

            _viewManager.OnOwnerArranged();

            for (var i = 0; i < Children.Count; ++i)
            {
                if (Children[i] is not FrameworkElement element)
                {
                    continue;
                }

                var virtInfo = GetVirtualizationInfo(element);
                virtInfo.KeepAlive = false;

                if (virtInfo.Owner == ElementOwner.ElementFactory || virtInfo.Owner == ElementOwner.PinnedPool)
                {
                    element.Arrange(new Rect(
                        ClearedElementsArrangePosition.X - element.DesiredSize.Width,
                        ClearedElementsArrangePosition.Y - element.DesiredSize.Height,
                        0,
                        0).ToNative());
                }
                else
                {
                    var newBounds = new Rect(element.ActualOffset.X, element.ActualOffset.Y, element.ActualWidth, element.ActualHeight);
                    virtInfo.ArrangeBounds = newBounds;
                    _viewportManager.RegisterScrollAnchorCandidate(element, virtInfo);
                }
            }

            _viewportManager.OnOwnerArranged();
            arrangeCompleted = true;
            return arrangeSize.ToNative();
        }
        finally
        {
            _isLayoutInProgress = false;
            if (arrangeCompleted)
            {
                ItemsRepeaterDiagnostics.RecordArrange(
                    ItemsRepeaterDiagnostics.GetElapsedMilliseconds(arrangeTimestamp),
                    layoutId,
                    itemCount,
                    GetRealizedCountForDiagnostics(layout));
            }
        }
    }

    protected override void OnBringIntoViewRequested(BringIntoViewRequestedEventArgs e)
    {
        base.OnBringIntoViewRequested(e);
        _viewportManager.OnBringIntoViewRequested(e);
    }

    internal FrameworkElement GetElementImpl(int index, bool forceCreate, bool suppressAutoRecycle)
    {
        return _viewManager.GetElement(index, forceCreate, suppressAutoRecycle);
    }

    internal void ClearElementImpl(FrameworkElement element)
    {
        var isClearedDueToCollectionChange =
            _processingItemsSourceChange is not null &&
            (_processingItemsSourceChange.Action == NotifyCollectionChangedAction.Remove ||
             _processingItemsSourceChange.Action == NotifyCollectionChangedAction.Replace ||
             _processingItemsSourceChange.Action == NotifyCollectionChangedAction.Reset);

        ItemsRepeaterDiagnostics.RecordElementRecycled(1);
        _viewManager.ClearElement(element, isClearedDueToCollectionChange);
        _viewportManager.OnElementCleared(element, GetVirtualizationInfo(element));
    }

    internal void OnElementPrepared(FrameworkElement element, VirtualizationInfo virtInfo)
    {
        _viewportManager.OnElementPrepared(element, virtInfo);

        if (ElementPrepared is not null)
        {
            _elementPreparedArgs ??= new ItemsRepeaterElementPreparedEventArgs(element, virtInfo.Index);
            _elementPreparedArgs.Update(element, virtInfo.Index);
            ElementPrepared(this, _elementPreparedArgs);
        }

        _childIndexChanged?.Invoke(this, new ChildIndexChangedEventArgs(element, virtInfo.Index));
    }

    internal void OnElementClearing(FrameworkElement element)
    {
        if (ElementClearing is not null)
        {
            _elementClearingArgs ??= new ItemsRepeaterElementClearingEventArgs(element);
            _elementClearingArgs.Update(element);
            ElementClearing(this, _elementClearingArgs);
        }

        _childIndexChanged?.Invoke(this, new ChildIndexChangedEventArgs(element, -1));
    }

    internal void OnElementIndexChanged(FrameworkElement element, int oldIndex, int newIndex)
    {
        if (ElementIndexChanged is not null)
        {
            _elementIndexChangedArgs ??= new ItemsRepeaterElementIndexChangedEventArgs(element, oldIndex, newIndex);
            _elementIndexChangedArgs.Update(element, oldIndex, newIndex);
            ElementIndexChanged(this, _elementIndexChangedArgs);
        }

        _childIndexChanged?.Invoke(this, new ChildIndexChangedEventArgs(element, newIndex));
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        _viewportManager.ResetScrollers();
        InvalidateMeasure();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        _viewportManager.ResetScrollers();
    }

    private static void OnDependencyPropertyChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        ((ItemsRepeater)sender).OnDependencyPropertyChanged(args);
    }

    private void OnDependencyPropertyChanged(DependencyPropertyChangedEventArgs change)
    {
        if (change.Property == ItemsSourceProperty)
        {
            var newSource = change.NewValue as IEnumerable;
            OnDataSourcePropertyChanged(ItemsSourceView, newSource is Microsoft.UI.Xaml.Controls.ItemsSourceView sourceView ? sourceView : newSource is null ? null : new Microsoft.UI.Xaml.Controls.ItemsSourceView(newSource));
        }
        else if (change.Property == ItemTemplateProperty)
        {
            OnItemTemplateChanged(change.OldValue, change.NewValue);
        }
        else if (change.Property == LayoutProperty)
        {
            OnLayoutChanged(change.OldValue as AttachedLayout, change.NewValue as AttachedLayout);
        }
        else if (change.Property == HorizontalCacheLengthProperty)
        {
            _viewportManager.HorizontalCacheLength = (double)change.NewValue;
        }
        else if (change.Property == VerticalCacheLengthProperty)
        {
            _viewportManager.VerticalCacheLength = (double)change.NewValue;
        }
        else if (change.Property == IsLogicalScrollEnabledProperty)
        {
            UpdateLogicalScrollingState();
        }
    }

    private int GetElementIndexImpl(UIElement element)
    {
        return element is FrameworkElement frameworkElement && frameworkElement.Parent == this
            ? _viewManager.GetElementIndex(TryGetVirtualizationInfo(frameworkElement))
            : -1;
    }

    private UIElement? GetElementFromIndexImpl(int index)
    {
        for (var i = 0; i < Children.Count; ++i)
        {
            if (Children[i] is FrameworkElement element)
            {
                var virtInfo = TryGetVirtualizationInfo(element);
                if (virtInfo?.IsRealized == true && virtInfo.Index == index)
                {
                    return element;
                }
            }
        }

        return null;
    }

    private UIElement GetOrCreateElementImpl(int index)
    {
        if (index >= 0 && index >= (ItemsSourceView?.Count ?? 0))
        {
            throw new ArgumentException("Argument index is invalid.", nameof(index));
        }

        if (_isLayoutInProgress)
        {
            throw new InvalidOperationException("GetOrCreateElement invocation is not allowed during layout.");
        }

        var element = GetElementFromIndexImpl(index);
        var isAnchorOutsideRealizedRange = element is null;

        if (isAnchorOutsideRealizedRange)
        {
            if (Layout is null)
            {
                throw new InvalidOperationException("Cannot make an anchor when there is no attached layout.");
            }

            element = LayoutContext.GetOrCreateElementAt(index);
            ((FrameworkElement)element).Measure(Size.Infinity);
        }

        _viewportManager.OnMakeAnchor((FrameworkElement)element!, isAnchorOutsideRealizedRange);
        ItemsRepeaterDiagnostics.RecordMakeAnchor(GetLayoutId(), index);
        InvalidateMeasure();
        return element!;
    }

    private void OnDataSourcePropertyChanged(Microsoft.UI.Xaml.Controls.ItemsSourceView? oldValue, Microsoft.UI.Xaml.Controls.ItemsSourceView? newValue)
    {
        if (_isLayoutInProgress)
        {
            throw new InvalidOperationException("Cannot set ItemsSourceView during layout.");
        }

        if (oldValue is not null)
        {
            oldValue.CollectionChanged -= OnItemsSourceViewChanged;
        }

        ItemsSourceView = newValue;

        if (newValue is not null)
        {
            newValue.CollectionChanged += OnItemsSourceViewChanged;
        }

        if (Layout is not null)
        {
            var args = new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset);
            _processingItemsSourceChange = args;

            try
            {
                if (Layout is VirtualizingLayout virtualLayout)
                {
                    virtualLayout.OnItemsChangedCore(LayoutContext, newValue, args);
                }
                else if (Layout is NonVirtualizingLayout)
                {
                    for (var i = 0; i < Children.Count; ++i)
                    {
                        if (Children[i] is FrameworkElement element && GetVirtualizationInfo(element).IsRealized)
                        {
                            ClearElementImpl(element);
                        }
                    }

                    Children.Clear();
                }
            }
            finally
            {
                _processingItemsSourceChange = null;
            }

            InvalidateMeasure();
        }
    }

    private void OnItemTemplateChanged(object? oldValue, object? newValue)
    {
        if (_isLayoutInProgress && oldValue is not null)
        {
            throw new InvalidOperationException("ItemTemplate cannot be changed during layout.");
        }

        if (Layout is not null)
        {
            var args = new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset);
            _processingItemsSourceChange = args;

            try
            {
                if (Layout is VirtualizingLayout virtualLayout)
                {
                    virtualLayout.OnItemsChangedCore(LayoutContext, newValue, args);
                }
                else if (Layout is NonVirtualizingLayout)
                {
                    for (var i = 0; i < Children.Count; ++i)
                    {
                        if (Children[i] is FrameworkElement element && GetVirtualizationInfo(element).IsRealized)
                        {
                            ClearElementImpl(element);
                        }
                    }
                }
            }
            finally
            {
                _processingItemsSourceChange = null;
            }
        }

        ItemTemplateShim = NormalizeItemTemplate(newValue);
        InvalidateMeasure();
    }

    private void OnLayoutChanged(AttachedLayout? oldValue, AttachedLayout? newValue)
    {
        if (_isLayoutInProgress)
        {
            throw new InvalidOperationException("Layout cannot be changed during layout.");
        }

        _viewManager.OnLayoutChanging();

        if (oldValue is not null)
        {
            UninitializeLayout(oldValue);
            oldValue.MeasureInvalidated -= InvalidateMeasureForLayout;
            oldValue.ArrangeInvalidated -= InvalidateArrangeForLayout;

            for (var i = 0; i < Children.Count; ++i)
            {
                if (Children[i] is FrameworkElement element && GetVirtualizationInfo(element).IsRealized)
                {
                    ClearElementImpl(element);
                }
            }

            LayoutState = null;
        }

        if (newValue is not null)
        {
            InitializeLayout(newValue);
            newValue.MeasureInvalidated += InvalidateMeasureForLayout;
            newValue.ArrangeInvalidated += InvalidateArrangeForLayout;
        }

        _scrollSizeCacheValid = false;
        _viewportManager.OnLayoutChanged(newValue is VirtualizingLayout);
        InvalidateMeasure();
    }

    private void OnItemsSourceViewChanged(object? sender, NotifyCollectionChangedEventArgs args)
    {
        if (_isLayoutInProgress)
        {
            throw new InvalidOperationException("Changes in data source are not allowed during layout.");
        }

        if (args.Action == NotifyCollectionChangedAction.Move)
        {
            OnItemsSourceViewChanged(sender, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, args.OldItems, args.OldStartingIndex));
            OnItemsSourceViewChanged(sender, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, args.NewItems, args.NewStartingIndex));
            return;
        }

        if (IsProcessingCollectionChange)
        {
            throw new InvalidOperationException("Changes in the data source are not allowed during another change in the data source.");
        }

        _processingItemsSourceChange = args;

        try
        {
            _viewManager.OnItemsSourceChanged(sender, args);

            if (Layout is VirtualizingLayout virtualLayout)
            {
                virtualLayout.OnItemsChangedCore(LayoutContext, sender, args);
            }
            else
            {
                InvalidateMeasure();
            }
        }
        finally
        {
            _processingItemsSourceChange = null;
        }
    }

    private void InvalidateMeasureForLayout(Microsoft.UI.Xaml.Controls.Layout sender, object args)
    {
        _ = sender;
        _ = args;
        _scrollSizeCacheValid = false;
        InvalidateMeasure();
    }

    private void InvalidateArrangeForLayout(Microsoft.UI.Xaml.Controls.Layout sender, object args)
    {
        _ = sender;
        _ = args;
        _scrollSizeCacheValid = false;
        InvalidateArrange();
    }

    private static IElementFactory? NormalizeItemTemplate(object? value)
    {
        return value switch
        {
            null => null,
            IElementFactory elementFactory => elementFactory,
            IDataTemplate dataTemplate => new ItemTemplateWrapper(dataTemplate),
            DataTemplate nativeTemplate => new ItemTemplateWrapper(nativeTemplate),
            _ => throw new InvalidOperationException("Unsupported item template."),
        };
    }

    private void InitializeLayout(AttachedLayout layout)
    {
        if (layout is VirtualizingLayout virtualLayout)
        {
            virtualLayout.InitializeForContextCore(LayoutContext);
        }
        else if (layout is NonVirtualizingLayout nonVirtualizingLayout)
        {
            nonVirtualizingLayout.InitializeForContextCore(LayoutContext.GetNonVirtualizingContextAdapter());
        }
    }

    private void UninitializeLayout(AttachedLayout layout)
    {
        if (layout is VirtualizingLayout virtualLayout)
        {
            virtualLayout.UninitializeForContextCore(LayoutContext);
        }
        else if (layout is NonVirtualizingLayout nonVirtualizingLayout)
        {
            nonVirtualizingLayout.UninitializeForContextCore(LayoutContext.GetNonVirtualizingContextAdapter());
        }
    }

    private static Size MeasureLayout(AttachedLayout layout, RepeaterLayoutContext context, Size availableSize)
    {
        return layout switch
        {
            VirtualizingLayout virtualLayout => virtualLayout.MeasureOverride(context, availableSize),
            NonVirtualizingLayout nonVirtualizingLayout => nonVirtualizingLayout.MeasureOverride(context.GetNonVirtualizingContextAdapter(), availableSize),
            _ => default,
        };
    }

    private static Size ArrangeLayout(AttachedLayout layout, RepeaterLayoutContext context, Size finalSize)
    {
        return layout switch
        {
            VirtualizingLayout virtualLayout => virtualLayout.ArrangeOverride(context, finalSize),
            NonVirtualizingLayout nonVirtualizingLayout => nonVirtualizingLayout.ArrangeOverride(context.GetNonVirtualizingContextAdapter(), finalSize),
            _ => finalSize,
        };
    }

    private string? GetLayoutId() => Layout?.LayoutId ?? Layout?.GetType().Name;

    private int GetRealizedCountForDiagnostics(AttachedLayout? layout)
    {
        if (layout is null)
        {
            return 0;
        }

        if (layout is NonVirtualizingLayout)
        {
            return Children.Count;
        }

        var count = 0;
        for (var i = 0; i < Children.Count; ++i)
        {
            if (Children[i] is FrameworkElement child && TryGetVirtualizationInfo(child)?.IsRealized == true)
            {
                ++count;
            }
        }

        return count;
    }
}
